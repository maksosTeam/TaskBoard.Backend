using SharedLibrary.Models;

namespace Kafka.Messaging.Contracts.Response;

public class GetItemByIdResponse
{
    public Guid CorrelationId { get; set; }

    public ItemModel? Item { get; set; }
}