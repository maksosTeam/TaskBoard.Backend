using Kafka.Messaging.Models;
using Kafka.Messaging.Services.Abstractions;
using ProjectService.BusinessLayer.Abstractions;
using SharedLibrary.Models;

namespace ProjectService.Kafka.Handlers
{
    public class GetItemByIdRequestHandler(
        IItemManager itemManager,
        IKafkaProducer<RpcMessage<ItemModel>> responseProducer)
        : IMessageHandler<RpcMessage<GetItemByIdRequest>>
    {
        public async Task HandleAsync(RpcMessage<GetItemByIdRequest> message, CancellationToken cancellationToken)
        {
            var item = await itemManager.GetByIdAsync(message.Payload.ItemId);

            var response = new RpcMessage<ItemModel>
            {
                CorrelationId = message.CorrelationId,
                Payload = item
            };
            await responseProducer.ProduceAsync(response, cancellationToken);
        }
    }
}
