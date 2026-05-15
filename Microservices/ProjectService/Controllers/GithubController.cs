using Microsoft.AspNetCore.Mvc;
using ProjectService.BusinessLayer.Abstractions;
using ProjectService.Models;

namespace ProjectService.Controllers;

[ApiController]
[Route("github")]
public class GithubController : ControllerBase
{
    private readonly IGitHubWebhookService _webhookService;

    public GithubController(IGitHubWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromHeader(Name = "X-GitHub-Event")] string gitHubEvent,
        [FromBody] GitHubWebhookPayload payload)
    {
        if (gitHubEvent != "pull_request")
        {
            return Ok("Event ignored"); 
        }

        if (payload.PullRequest == null)
        {
            return BadRequest("Invalid payload: PullRequest data is missing");
        }

        var isProcessed = await _webhookService.ProcessPullRequestAsync(
            payload.Action,
            payload.PullRequest.Title,
            payload.PullRequest.HtmlUrl
        );

        return Ok(isProcessed
            ? "Webhook processed and task updated." 
            : "Webhook received, but no actions performed (either ignored action or no task key found).");
    }
}