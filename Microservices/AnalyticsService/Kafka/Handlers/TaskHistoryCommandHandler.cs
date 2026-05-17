using AnalyticsService.BusinessLayer.Implementations;
using Kafka.Messaging.Services.Abstractions;
using SharedLibrary.Models;
using SharedLibrary.Models.AnalyticModels;

namespace AnalyticsService.Kafka.Handlers
{
    public class TaskHistoryCommandHandler(ITaskManager taskManager)
        : IMessageHandler<RpcMessage<SharedLibrary.Models.AnalyticModels.TaskHistoryModel>>
    {
        public async Task HandleAsync(RpcMessage<SharedLibrary.Models.AnalyticModels.TaskHistoryModel> message, CancellationToken cancellationToken)
        {
            await taskManager.CreateAsync(message.Payload);
        }
    }
}
