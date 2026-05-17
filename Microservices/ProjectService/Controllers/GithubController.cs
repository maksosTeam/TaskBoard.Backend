using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using ProjectService.BusinessLayer.Abstractions;
using ProjectService.Models;

namespace ProjectService.Controllers;

[ApiController]
[Route("github")]
public class GithubController : ControllerBase
{
    private readonly IGitHubWebhookService _webhookService;
    private readonly string _webhookSecret;

    public GithubController(IGitHubWebhookService webhookService, IConfiguration configuration)
    {
        _webhookService = webhookService;
        _webhookSecret = Environment.GetEnvironmentVariable("GITHUB_WEBHOOK_SECRET") 
                         ?? configuration["GitHub:WebhookSecret"]!;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromHeader(Name = "X-GitHub-Event")] string gitHubEvent,
        [FromHeader(Name = "X-Hub-Signature-256")] string signature,
        [FromBody] GitHubWebhookPayload payload)
    {
        Console.WriteLine("START WEBHOOK");

        if (string.IsNullOrEmpty(signature))
        {
            return Unauthorized("Missing signature");
        }

        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var jsonBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (!VerifyGitHubSignature(jsonBody, signature, _webhookSecret))
        {
            return Unauthorized("Invalid signature");
        }

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

    private static bool VerifyGitHubSignature(string body, string signature, string secret)
    {
        if (!signature.StartsWith("sha256=")) return false;

        var expectedSignature = signature["sha256=".Length..];
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA256(secretBytes);
        var hashBytes = hmac.ComputeHash(bodyBytes);
        var computedSignature = Convert.ToHexString(hashBytes).ToLower();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature), 
            Encoding.UTF8.GetBytes(expectedSignature)
        );
    }
}