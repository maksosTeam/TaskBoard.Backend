namespace Kafka.Messaging.Contracts.Requests;

public class GetItemByIdRequest
{
    public Guid CorrelationId { get; set; }

    public int ItemId { get; set; }
}