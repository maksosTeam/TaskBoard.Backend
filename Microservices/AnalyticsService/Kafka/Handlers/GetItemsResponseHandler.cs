using Kafka.Messaging.Services.Abstractions;
using SharedLibrary.Models;

namespace AnalyticsService.Kafka.Handlers
{
    public class GetItemsResponseHandler(KafkaResponseTracker<IEnumerable<ItemModel>> tracker)
        : IMessageHandler<RpcMessage<IEnumerable<ItemModel>>>
    {
        public Task HandleAsync(RpcMessage<IEnumerable<ItemModel>> message, CancellationToken cancellationToken)
        {
            tracker.Resolve(message.CorrelationId, message.Payload);
            return Task.CompletedTask;
        }
    }
}
