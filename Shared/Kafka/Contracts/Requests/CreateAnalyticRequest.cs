using SharedLibrary.Models;

namespace Kafka.Messaging.Contracts.Requests;

public class CreateAnalyticRequest
{
    public Guid CorrelationId { get; set; }
    public ItemModel Model { get; set; }
}