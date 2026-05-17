using AnalyticsService.BusinessLayer.Abstractions;
using AnalyticsService.DataLayer.Abstractions;
using AnalyticsService.Kafka;
using Kafka.Messaging.Models;
using Kafka.Messaging.Services.Abstractions;
using SharedLibrary.Entities.AnalyticsService;
using SharedLibrary.Models;
using SharedLibrary.Models.AnalyticModels;
using SharedLibrary.Models.KafkaModel;

namespace AnalyticsService.BusinessLayer.Implementations
{
    public class TaskManager(
        IKafkaProducer<RpcMessage<ProjectIdRequest>> projectItemsProducer,
        IKafkaProducer<RpcMessage<ItemIdRequest>> singleItemProducer,
        KafkaResponseTracker<IEnumerable<ItemModel>> itemsTracker,
        KafkaResponseTracker<ItemModel> singleItemTracker,
        ITaskHistoryRepository taskHistoryRepository) : ITaskManager
    {
        // Универсальный метод для отправки запроса и ожидания ответа
        private async Task<TResponse> FetchFromKafkaAsync<TRequest, TResponse>(
            TRequest requestPayload,
            IKafkaProducer<RpcMessage<TRequest>> producer,
            KafkaResponseTracker<TResponse> tracker,
            CancellationToken cancellationToken = default)
        {
            var requestMessage = new RpcMessage<TRequest> { Payload = requestPayload };
            var responseTask = tracker.WaitAsync(requestMessage.CorrelationId, cancellationToken);

            await producer.ProduceAsync(requestMessage, cancellationToken);
            return await responseTask;
        }

        public async Task<int> CreateAsync(SharedLibrary.Models.AnalyticModels.TaskHistoryModel model)
        {
            var entity = new TaskHistoryEntity
            {
                ChangedAt = model.ChangedAt,
                UserId = model.UserId,
                OldValue = model.OldValue,
                NewValue = model.NewValue,
                FieldName = model.FieldName,
                ItemId = model.ItemId
            };
            await taskHistoryRepository.CreateAsync(entity);
            return entity.Id;
        }

        public async Task<IEnumerable<ItemModel>> GetCompletedTaskBetween(int projectId, DateTime startDate, DateTime endDate)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var items = await FetchFromKafkaAsync(
                new ProjectIdRequest { ProjectId = projectId },
                projectItemsProducer,
                itemsTracker,
                cts.Token);

            return items.Where(item => IsTaskCompleted(item) && IsTaskBetweenDates(item, startDate, endDate));
        }

        public async Task<int> GetCompletedTaskCountBetween(int projectId, DateTime startDate, DateTime endDate)
        {
            return (await GetCompletedTaskBetween(projectId, startDate, endDate)).Count();
        }

        public async Task<TimeSpan?> GetAverageTimeInStatusAsync(int taskId, string statusName)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var item = await FetchFromKafkaAsync(
                new ItemIdRequest { ItemId = taskId },
                singleItemProducer,
                singleItemTracker,
                cts.Token);

            var start = item.StartDate;

            var history = (await taskHistoryRepository.GetHistoryByTaskIdAsync(taskId))
                .Where(h => h.FieldName == "Status")
                .OrderBy(h => h.ChangedAt)
                .ToList();

            var totalTime = TimeSpan.Zero;
            var count = 0;

            foreach (var entry in history)
            {
                if (entry.OldValue == statusName)
                {
                    var duration = entry.ChangedAt - start;
                    totalTime += duration;
                    count++;
                }
                start = entry.ChangedAt;
            }

            var lastStatus = history.LastOrDefault()?.NewValue ?? "Unknown";
            if (lastStatus == statusName)
            {
                var endTime = item.ExpectedEndDate;
                var duration = endTime - start;
                totalTime += duration;
                count++;
            }

            if (count == 0)
                return null;

            return totalTime / count;
        }

        public async Task<TimeSpan> GetTotalTimeOutsideStatusAsync(int taskId, string excludedStatus)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var item = await FetchFromKafkaAsync(
                new ItemIdRequest { ItemId = taskId },
                singleItemProducer,
                singleItemTracker,
                cts.Token);

            var history = (await taskHistoryRepository.GetHistoryByTaskIdAsync(taskId))
                .Where(h => h.FieldName == "StatusId")
                .OrderBy(h => h.ChangedAt)
                .ToList();

            var total = TimeSpan.Zero;
            var current = item.StartDate;
            var lastStatus = "";

            foreach (var h in history)
            {
                if (lastStatus != excludedStatus)
                    total += h.ChangedAt - current;

                current = h.ChangedAt;
                lastStatus = h.NewValue;
            }

            if (lastStatus != excludedStatus)
                total += item.ExpectedEndDate - current;

            return total;
        }

        public async Task<IDictionary<string, TimeSpan>> GetAverageTimeInStatusesAsync(int taskId)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var item = await FetchFromKafkaAsync(
                new ItemIdRequest { ItemId = taskId },
                singleItemProducer,
                singleItemTracker,
                cts.Token);

            var start = item.StartDate;
            var timeSpent = new Dictionary<string, TimeSpan>();
            var count = new Dictionary<string, int>();

            var history = (await taskHistoryRepository.GetHistoryByTaskIdAsync(taskId))
                .Where(h => h.FieldName == "StatusId")
                .OrderBy(h => h.ChangedAt)
                .ToList();

            var currentStatus = history.FirstOrDefault()?.OldValue ?? "Unknown";

            foreach (var entry in history)
            {
                var duration = entry.ChangedAt - start;

                if (timeSpent.TryAdd(currentStatus, duration))
                    count[currentStatus] = 1;
                else
                {
                    timeSpent[currentStatus] += duration;
                    count[currentStatus]++;
                }

                currentStatus = entry.NewValue;
                start = entry.ChangedAt;
            }

            var lastDuration = item.ExpectedEndDate - start;

            if (timeSpent.TryAdd(currentStatus, lastDuration))
                count[currentStatus] = 1;
            else
            {
                timeSpent[currentStatus] += lastDuration;
                count[currentStatus]++;
            }

            var result = new Dictionary<string, TimeSpan>();
            foreach (var status in timeSpent.Keys)
            {
                var average = timeSpent[status] / count[status];
                result.Add(status, average);
            }

            return result;
        }

        private static bool IsTaskCompleted(ItemModel itemModel)
        {
            return itemModel.Status is not null && itemModel.Status.Name.Equals("Готово");
        }

        private static bool IsTaskBetweenDates(ItemModel itemModel, DateTime startDate, DateTime endDate)
        {
            return itemModel.StartDate >= startDate && itemModel.ExpectedEndDate <= endDate;
        }
    }
}