namespace Kafka.Messaging.Contracts.Requests;

public class GetProjectByIdRequest
{
    public Guid CorrelationId { get; set; }

    public int ProjectId { get; set; }
}