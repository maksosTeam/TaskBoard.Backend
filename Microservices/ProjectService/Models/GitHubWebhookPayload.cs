using System.Text.Json.Serialization;

namespace ProjectService.Models;

public record GitHubWebhookPayload(
    [property: JsonPropertyName("action")] string Action, // opened, closed, reopened
    [property: JsonPropertyName("pull_request")] GitHubPullRequest PullRequest
);

public record GitHubPullRequest(
    [property: JsonPropertyName("title")] string Title,         // Название PR, где ищем "asd-123"
    [property: JsonPropertyName("html_url")] string HtmlUrl,   // Ссылка на сам PR на гитхабе
    [property: JsonPropertyName("state")] string State         // open, closed
);