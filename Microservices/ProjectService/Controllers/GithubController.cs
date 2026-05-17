using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        _webhookSecret = configuration["GitHub:WebhookSecret"]
                         ?? throw new InvalidOperationException("GitHub Webhook Secret is not configured.");
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromHeader(Name = "X-GitHub-Event")] string gitHubEvent,
        [FromHeader(Name = "X-Hub-Signature-256")]
        string signature)
    {
        Console.WriteLine("START WEBHOOK");

        if (string.IsNullOrEmpty(signature)) return Unauthorized("Missing signature");

        // 1. Читаем тело ОДИН раз напрямую из стрима
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var jsonBody = await reader.ReadToEndAsync();

        // 2. Сразу проверяем подпись по сырому тексту
        if (!VerifyGitHubSignature(jsonBody, signature, _webhookSecret))
        {
            // Если падает тут, выведите в консоль jsonBody.Length, чтобы убедиться, что тело вообще доходит
            Console.WriteLine($"Signature verification failed. Body length: {jsonBody.Length}");
            return Unauthorized("Invalid signature");
        }

        if (gitHubEvent != "pull_request") return Ok("Event ignored");

        // 3. Десериализуем вручную, так как [FromBody] мы убрали
        GitHubWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<GitHubWebhookPayload>(jsonBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON Deserialization failed: {ex.Message}");
            return BadRequest("Invalid JSON payload");
        }

        if (payload == null || payload.PullRequest == null)
            return BadRequest("Invalid payload: PullRequest data is missing");

        // 4. Передаем данные в сервис (код сервиса у вас написан отлично, к нему вопросов нет)
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