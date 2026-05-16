namespace Kafka.Messaging.Contracts.Requests;

public class GetItemsByProjectRequest
{
    public Guid CorrelationId { get; set; }
    public int ProjectId { get; set; }
}