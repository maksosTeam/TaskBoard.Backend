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
        IKafkaProducer<RpcMessage<ProjectIdRequest>> generalProducer, // Для старых методов
        IKafkaProducer<RpcMessage<GetProjectItemsRequest>> itemsProducer, // Для списка задач в Burndown
        IKafkaProducer<RpcMessage<GetProjectByIdRequest>> projectProducer, // Для инфо о проекте в Burndown
        KafkaResponseTracker<IEnumerable<ItemModel>> itemsTracker,
        KafkaResponseTracker<ProjectModel> projectTracker,
        ITaskHistoryRepository taskHistoryRepository,
        IUserRepository userRepository) : IProjectManager
    {
        public async Task<BurndownChartModel> GetBurndown(BurnDownChartRequest request)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Передаем правильные продьюсеры и контракты запросов
            var itemsTask = FetchFromKafkaAsync(itemsProducer, new GetProjectItemsRequest { ProjectId = request.ProjectId }, itemsTracker, cts.Token);
            var projectTask = FetchFromKafkaAsync(projectProducer, new GetProjectByIdRequest { ProjectId = request.ProjectId }, projectTracker, cts.Token);

            // Ждем выполнения обоих запросов параллельно
            await Task.WhenAll(itemsTask, projectTask);

            // Безопасно получаем результаты без .Result
            var items = await itemsTask;
            var project = await projectTask;

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
            var items = await FetchFromKafkaAsync(generalProducer, new ProjectIdRequest { ProjectId = query.ProjectId }, itemsTracker, cts.Token);

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

        public async Task<ICollection<TaskHistoryModel>> GetProjectHistory(int projectId)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(generalProducer, new ProjectIdRequest { ProjectId = projectId }, itemsTracker, cts.Token);

            var itemIds = items.Select(x => x.Id).ToHashSet();
            var itemHistory = await taskHistoryRepository.GetHistoryByManyTaskIds(itemIds);

            var historyModels = await Task.WhenAll(
                                itemHistory.Select(i => TaskHistoryMapper.ToModel(i, userRepository)));

            return historyModels.OrderByDescending(x => x.ChangedAt).ToList();
        }

        public async Task<ICollection<GanttTaskModel>> GetGanttChartDataAsync(int projectId)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(generalProducer, new ProjectIdRequest { ProjectId = projectId }, itemsTracker, cts.Token);

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
            var items = await FetchFromKafkaAsync(generalProducer, new ProjectIdRequest { ProjectId = projectId }, itemsTracker, cts.Token);

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

        public async Task<List<HeatmapCell>> GetHeatmapData(HeatmapQueryModel query)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(generalProducer, new ProjectIdRequest { ProjectId = query.ProjectId }, itemsTracker, cts.Token);

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

        // Универсальный метод отправки RPC-запросов в Kafka
        private async Task<TResponse> FetchFromKafkaAsync<TRequest, TResponse>(
            IKafkaProducer<RpcMessage<TRequest>> producer,
            TRequest req,
            KafkaResponseTracker<TResponse> tracker,
            CancellationToken cancellationToken = default)
        {
            var requestMessage = new RpcMessage<TRequest>
            {
                Payload = req
            };

            var responseTask = tracker.WaitAsync(requestMessage.CorrelationId, cancellationToken);

            // Используем переданный конкретный продьюсер
            await producer.ProduceAsync(requestMessage, cancellationToken);

            return await responseTask;
        }
    }
}