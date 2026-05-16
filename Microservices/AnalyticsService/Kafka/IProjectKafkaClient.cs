using SharedLibrary.Models;
using SharedLibrary.ProjectModels;

namespace AnalyticsService.Kafka;

public interface IProjectKafkaClient
{
    Task<IEnumerable<ItemModel>> GetItemsAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<ItemModel?> GetItemByIdAsync(
        int itemId,
        CancellationToken cancellationToken = default);

    Task<ProjectModel?> GetProjectByIdAsync(
        int projectId,
        CancellationToken cancellationToken = default);
}