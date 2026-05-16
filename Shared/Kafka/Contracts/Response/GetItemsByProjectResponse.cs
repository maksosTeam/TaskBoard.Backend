using SharedLibrary.Models;

namespace Kafka.Messaging.Contracts.Response;

public class GetItemsByProjectResponse
{
    public Guid CorrelationId { get; set; }
    public IEnumerable<ItemModel> Items { get; set; }
}