namespace Kafka.Messaging.Contracts.Response;

public class CreateAnalyticResponse
{
    public Guid CorrelationId { get; set; }
    public int Id { get; set; }
}