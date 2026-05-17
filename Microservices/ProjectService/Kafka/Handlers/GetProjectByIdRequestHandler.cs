using Kafka.Messaging.Models;
using Kafka.Messaging.Services.Abstractions;
using ProjectService.BusinessLayer.Abstractions;
using SharedLibrary.Models;
using SharedLibrary.ProjectModels;

namespace ProjectService.Kafka.Handlers
{
    public class GetProjectByIdRequestHandler(
        IProjectManager projectManager,
        IKafkaProducer<RpcMessage<ProjectModel>> responseProducer)
        : IMessageHandler<RpcMessage<GetProjectByIdRequest>>
    {
        public async Task HandleAsync(RpcMessage<GetProjectByIdRequest> message, CancellationToken cancellationToken)
        {
            var project = await projectManager.GetByIdAsync(message.Payload.ProjectId);

            var response = new RpcMessage<ProjectModel>
            {
                CorrelationId = message.CorrelationId,
                Payload = project
            };
            await responseProducer.ProduceAsync(response, cancellationToken);
        }
    }
}
