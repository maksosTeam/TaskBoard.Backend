using Kafka.Messaging.Models;
using Kafka.Messaging.Services.Abstractions;
using ProjectService.BusinessLayer.Abstractions;
using SharedLibrary.Models;

namespace ProjectService.Kafka.Handlers
{
    public class GetProjectItemsRequestHandler(
        IItemManager itemManager,
        IKafkaProducer<RpcMessage<IEnumerable<ItemModel>>> responseProducer)
        : IMessageHandler<RpcMessage<GetProjectItemsRequest>>
    {
        public async Task HandleAsync(RpcMessage<GetProjectItemsRequest> message, CancellationToken cancellationToken)
        {
            var items = await itemManager.GetByProjectIdAsync(message.Payload.ProjectId);

            var response = new RpcMessage<IEnumerable<ItemModel>>
            {
                CorrelationId = message.CorrelationId,
                Payload = items
            };
            await responseProducer.ProduceAsync(response, cancellationToken);
        }
    }
}
