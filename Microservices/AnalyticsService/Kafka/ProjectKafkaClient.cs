using AnalyticsService.Kafka.Consumers;
using AnalyticsService.Kafka.Requests;
using Kafka.Messaging;
using Kafka.Messaging.Contracts.Requests;
using Kafka.Messaging.Contracts.Response;
using SharedLibrary.Models;
using SharedLibrary.ProjectModels;

namespace AnalyticsService.Kafka;

public class ProjectKafkaClient(
    IKafkaProducer<GetItemsByProjectRequest> getItemsByProjectProducer,
    IKafkaProducer<GetItemByIdRequest> getItemByIdProducer, 
    IKafkaProducer<GetProjectByIdRequest> getProjectByIdProducer) 
    : IProjectKafkaClient
{
    public async Task<IEnumerable<ItemModel>> GetItemsAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();

        var tcs =
            new TaskCompletionSource<GetItemsByProjectResponse>();

        PendingRequests.Requests.TryAdd(correlationId, tcs);

        await getItemsByProjectProducer.ProduceAsync(
            new GetItemsByProjectRequest
            {
                CorrelationId = correlationId,
                ProjectId = projectId
            },
            cancellationToken);

        var completedTask = await Task.WhenAny(
            tcs.Task,
            Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));

        if (completedTask != tcs.Task)
        {
            PendingRequests.Requests.TryRemove(
                correlationId,
                out _);

            throw new TimeoutException();
        }

        var response = await tcs.Task;

        return response.Items;
    }
    
    public async Task<ItemModel?> GetItemByIdAsync(
        int itemId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();

        var tcs =
            new TaskCompletionSource<GetItemByIdResponse>();

        PendingItemRequests.Requests.TryAdd(
            correlationId,
            tcs);

        await getItemByIdProducer.ProduceAsync(
            new GetItemByIdRequest
            {
                CorrelationId = correlationId,
                ItemId = itemId
            },
            cancellationToken);

        var completedTask = await Task.WhenAny(
            tcs.Task,
            Task.Delay(TimeSpan.FromSeconds(30),
                cancellationToken));

        if (completedTask != tcs.Task)
        {
            PendingItemRequests.Requests.TryRemove(
                correlationId,
                out _);

            throw new TimeoutException();
        }

        var response = await tcs.Task;

        return response.Item;
    }
    
    public async Task<ProjectModel?> GetProjectByIdAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();

        var tcs =
            new TaskCompletionSource<GetProjectByIdResponse>();

        PendingProjectRequests.Requests.TryAdd(
            correlationId,
            tcs);

        await getProjectByIdProducer.ProduceAsync(
            new GetProjectByIdRequest
            {
                CorrelationId = correlationId,
                ProjectId = projectId
            },
            cancellationToken);

        var completedTask = await Task.WhenAny(
            tcs.Task,
            Task.Delay(
                TimeSpan.FromSeconds(30),
                cancellationToken));

        if (completedTask != tcs.Task)
        {
            PendingProjectRequests.Requests.TryRemove(
                correlationId,
                out _);

            throw new TimeoutException();
        }

        var response = await tcs.Task;

        return response.Project;
    }
}