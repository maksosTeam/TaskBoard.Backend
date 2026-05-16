using SharedLibrary.ProjectModels;

namespace Kafka.Messaging.Contracts.Response;

public class GetProjectByIdResponse
{
    public Guid CorrelationId { get; set; }

    public ProjectModel? Project { get; set; }
}