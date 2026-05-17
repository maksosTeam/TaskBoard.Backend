using Kafka.Messaging.Services.Abstractions;
using SharedLibrary.Models;
using SharedLibrary.ProjectModels;

namespace AnalyticsService.Kafka.Handlers
{
    public class GetProjectResponseHandler(KafkaResponseTracker<ProjectModel> tracker)
        : IMessageHandler<RpcMessage<ProjectModel>>
    {
        public Task HandleAsync(RpcMessage<ProjectModel> message, CancellationToken cancellationToken)
        {
            tracker.Resolve(message.CorrelationId, message.Payload);
            return Task.CompletedTask;
        }
    }
}
