namespace ProjectService.BusinessLayer.Abstractions;

public interface IGitHubWebhookService
{
    Task<bool> ProcessPullRequestAsync(string action, string title, string htmlUrl, CancellationToken cancellationToken = default);
}