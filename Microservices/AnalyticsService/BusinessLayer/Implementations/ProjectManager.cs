using AnalyticsService.BusinessLayer.Abstractions;
using AnalyticsService.DataLayer.Abstractions;
using AnalyticsService.Kafka;
using AnalyticsService.Mapper;
using AnalyticsService.Models;
using Kafka.Messaging.Models;
using Kafka.Messaging.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Constants;
using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.Models;
using SharedLibrary.Models.AnalyticModels;
using SharedLibrary.ProjectModels;

namespace AnalyticsService.BusinessLayer.Implementations
{
    public class ProjectManager(
        IKafkaProducer<RpcMessage<ProjectIdRequest>> rpcProducer,
        KafkaResponseTracker<IEnumerable<ItemModel>> itemsTracker,
        KafkaResponseTracker<ProjectModel> projectTracker,
        ITaskHistoryRepository taskHistoryRepository,
        IUserRepository userRepository) : IProjectManager
    {
        public async Task<BurndownChartModel> GetBurndown(BurnDownChartRequest request)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var itemsTask = FetchFromKafkaAsync(new ProjectIdRequest { ProjectId = request.ProjectId }, itemsTracker, cts.Token);
            var projectTask = FetchFromKafkaAsync(new ProjectIdRequest { ProjectId = request.ProjectId }, projectTracker, cts.Token);

            await Task.WhenAll(itemsTask, projectTask);

            var items = itemsTask.Result;
            var project = projectTask.Result;

            if (items is null || project is null)
                throw new InvalidOperationException("Project service вернул пустой ответ или запрос завершился по таймауту.");

            var sprintStart = project.StartDate.Date;
            var sprintEnd = project.ExpectedEndDate.Date;

            var result = new BurndownChartModel
            {
                TasksCount = items.Count(),
                StartDate = sprintStart,
                EndDate = sprintEnd,
                TasksCountByDate = new Dictionary<DateTime, int>()
            };

            for (var date = sprintStart; date <= sprintEnd; date = date.AddDays(1))
            {
                var remainingTasks = items
                    .Where(item =>
                        item.StartDate.Date <= date &&
                        item.ExpectedEndDate.Date >= date &&
                        item.Status?.IsDone == false);

                if (request.Priority <= Priority.CRITICAL && request.Priority >= Priority.VERY_LOW)
                    remainingTasks = remainingTasks.Where(item => item.Priority == request.Priority);

                result.TasksCountByDate[date] = remainingTasks.Count();
            }

            return result;
        }

        public async Task<ICollection<ChartDataPoint>> GetCustomChart(ChartQueryModel query)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(new ProjectIdRequest { ProjectId = query.ProjectId }, itemsTracker, cts.Token);

            items = items.Where(i => i.StartDate >= query.Start && i.StartDate <= query.End).ToList();

            var yAxis = query.YAxis?.ToLower() ?? "count";

            List<ChartDataPoint> data;

            switch (query.XAxis.ToLower())
            {
                case "status":
                    data = items
                        .Where(i => i.Status != null)
                        .GroupBy(i => i.Status!.Name)
                        .Select(g => new ChartDataPoint
                        {
                            X = g.Key,
                            Y = CalculateYAxis(g, yAxis)
                        })
                        .ToList();
                    break;

                case "user":
                    var allContributors = items
                        .SelectMany(i => i.Contributors)
                        .Distinct()
                        .ToList();

                    if (items.Any(i => i.Contributors.Count == 0))
                    {
                        allContributors.Add("Без исполнителя");
                    }

                    data = allContributors
                        .Select(contributor => new ChartDataPoint
                        {
                            X = contributor,
                            Y = CalculateYAxis(
                                items.Where(i =>
                                    (contributor == "Без исполнителя" && i.Contributors.Count == 0) ||
                                    (contributor != "Без исполнителя" && i.Contributors.Contains(contributor))
                                ),
                                yAxis)
                        })
                        .ToList();
                    break;

                case "date":
                    data = items
                        .GroupBy(i => i.StartDate.Date)
                        .Select(g => new ChartDataPoint
                        {
                            X = g.Key.ToString("yyyy-MM-dd"),
                            Y = CalculateYAxis(g, yAxis)
                        })
                        .ToList();
                    break;

                default:
                    throw new ArgumentException($"Unsupported XAxis: {query.XAxis}");
            }

            return data;
        }

        private static double CalculateYAxis(IEnumerable<ItemModel> group, string yAxis)
        {
            return yAxis switch
            {
                "count" => group.Count(),
                "sum-priority" => group.Sum(i => i.Priority),
                "avg-duration" => group
                    .Select(i => (i.ExpectedEndDate - i.StartDate).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average(),
                _ => throw new ArgumentException($"Unsupported YAxis: {yAxis}")
            };
        }


        public async Task<ICollection<TaskHistoryModel>> GetProjectHistory(int projectId)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(new ProjectIdRequest { ProjectId = projectId }, itemsTracker, cts.Token);

            var itemIds = items.Select(x => x.Id).ToHashSet();
            var itemHistory = await taskHistoryRepository.GetHistoryByManyTaskIds(itemIds);

            var historyModels = await Task.WhenAll(
                                itemHistory.Select(i => TaskHistoryMapper.ToModel(i, userRepository)));

            return historyModels.OrderByDescending(x => x.ChangedAt).ToList();
        }

        public async Task<ICollection<GanttTaskModel>> GetGanttChartDataAsync(int projectId)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(new ProjectIdRequest { ProjectId = projectId }, itemsTracker, cts.Token);

            var ganttTasks = items
                .Where(i => i.StartDate != default && i.ExpectedEndDate != default)
                .Select(i => new GanttTaskModel
                {
                    Id = i.Id.ToString(),
                    Name = i.Title,
                    Start = i.StartDate.ToString("yyyy-MM-dd"),
                    End = i.ExpectedEndDate.ToString("yyyy-MM-dd"),
                    Parent = i.ParentId?.ToString(),
                    Status = i.Status?.Name,
                    Assignee = i.Contributors
                })
                .ToList();

            return ganttTasks;
        }

        public async Task<ICollection<RoadmapItemModel>> GetRoadmapDataAsync(int projectId)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(new ProjectIdRequest { ProjectId = projectId }, itemsTracker, cts.Token);

            var roadmapItems = items
                .Where(i => i.StartDate != default && i.ExpectedEndDate != default)
                .Select(i => new RoadmapItemModel
                {
                    Id = i.Id,
                    Title = i.Title,
                    Start = i.StartDate,
                    End = i.ExpectedEndDate,
                    Status = i.Status?.Name,
                    Assignees = i.Contributors
                })
                .ToList();

            return roadmapItems;
        }

        /// <summary>
        /// Получение данных для построения тепловой карты (heatmap)
        /// </summary>
        /// <remarks>
        /// <b>Описание метрик:</b>
        /// <ul>
        ///     <li><b>inflow</b> – количество задач, созданных в указанный день (по дате начала)</li>
        ///     <li><b>outflow</b> – количество завершённых задач в указанный день (по дате обновления и статусу "Завершено")</li>
        ///     <li><b>avg-duration</b> – средняя длительность задач (в днях) от начала до ожидаемого завершения в конкретный день</li>
        /// </ul>
        /// <b>Оси:</b>
        /// <ul>
        ///     <li><b>X</b> – дата (в формате YYYY-MM-DD)</li>
        ///     <li><b>Y</b> – исполнитель задачи (если не назначен – "Без исполнителя")</li>
        ///     <li><b>Value</b> – значение метрики (количество задач или средняя длительность)</li>
        /// </ul>
        /// </remarks>
        /// <param name="query">Параметры фильтрации: идентификатор проекта, диапазон дат, тип метрики</param>
        /// <returns>Список ячеек тепловой карты с координатами и значением</returns>
        public async Task<List<HeatmapCell>> GetHeatmapData(HeatmapQueryModel query)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(new ProjectIdRequest { ProjectId = query.ProjectId }, itemsTracker, cts.Token);

            var filteredItems = items
                .Where(i => i.StartDate.Date >= query.Start.Date && i.StartDate.Date <= query.End.Date)
                .ToList();

            return query.Metric.ToLower() switch
            {
                "inflow" => filteredItems
                    .SelectMany(i => i.Contributors.DefaultIfEmpty("Без исполнителя"),
                                (item, contributor) => new { contributor, date = item.StartDate.Date })
                    .GroupBy(g => new { g.date, g.contributor })
                    .Select(g => new HeatmapCell
                    {
                        X = g.Key.date.ToString("yyyy-MM-dd"),
                        Y = g.Key.contributor,
                        Value = g.Count()
                    }).ToList(),

                "outflow" => filteredItems
                    .Where(i => i.Status?.Name == "Завершено")
                    .SelectMany(i => i.Contributors.DefaultIfEmpty("Без исполнителя"),
                                (item, contributor) => new { contributor, date = item.UpdatedAt.Date })
                    .GroupBy(g => new { g.date, g.contributor })
                    .Select(g => new HeatmapCell
                    {
                        X = g.Key.date.ToString("yyyy-MM-dd"),
                        Y = g.Key.contributor,
                        Value = g.Count()
                    }).ToList(),

                "avg-duration" => filteredItems
                    .SelectMany(i => i.Contributors.DefaultIfEmpty("Без исполнителя"),
                                (item, contributor) => new
                                {
                                    contributor,
                                    date = item.StartDate.Date,
                                    duration = (item.ExpectedEndDate - item.StartDate).TotalDays
                                })
                    .GroupBy(g => new { g.date, g.contributor })
                    .Select(g => new HeatmapCell
                    {
                        X = g.Key.date.ToString("yyyy-MM-dd"),
                        Y = g.Key.contributor,
                        Value = Math.Round(g.Average(x => x.duration), 2)
                    }).ToList(),

                _ => throw new ArgumentException("Unknown metric: " + query.Metric)
            };
        }


        private async Task<TResponse> FetchFromKafkaAsync<TResponse>(
        ProjectIdRequest req,
        KafkaResponseTracker<TResponse> tracker,
        CancellationToken cancellationToken = default)
        {
            // Создаем сообщение, где Payload — это объект ProjectIdRequest
            var requestMessage = new RpcMessage<ProjectIdRequest>
            {
                Payload = req
            };

            // 1. Начинаем слушать ответ ДО отправки
            var responseTask = tracker.WaitAsync(requestMessage.CorrelationId, cancellationToken);

            // 2. Отправляем типизированный запрос в Кафку
            await rpcProducer.ProduceAsync(requestMessage, cancellationToken);

            // 3. Ждем ответа
            return await responseTask;
        }

    }
}