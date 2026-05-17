using Kafka.Messaging.Services.Abstractions;
using SharedLibrary.Models;

namespace AnalyticsService.Kafka.Handlers
{
    public class GetItemResponseHandler(KafkaResponseTracker<ItemModel> tracker)
        : IMessageHandler<RpcMessage<ItemModel>>
    {
        public Task HandleAsync(RpcMessage<ItemModel> message, CancellationToken cancellationToken)
        {
            tracker.Resolve(message.CorrelationId, message.Payload);
            return Task.CompletedTask;
        }
    }
}
