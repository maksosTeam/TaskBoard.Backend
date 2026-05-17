using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjectService.BusinessLayer.Abstractions;

namespace ProjectService.BusinessLayer.Implementations;

public class AiManager(HttpClient httpClient, IOptions<AiSettings> aiOptions) : IAiManager{

    private readonly string? _apiKey = aiOptions.Value.ApiKey;    
    private const string Url = "https://openrouter.ai/api/v1/chat/completions";
    private const string Model = "google/gemini-2.0-flash-001";

    /// <summary>
    /// Обрабатывает текст пользователя с помощью ИИ (перефразирование или детализация).
    /// </summary>
    public async Task<string> ProcessUserText(string userText, string mode = "paraphrase")
    {
        if (string.IsNullOrWhiteSpace(userText)) return "Текст для обработки не может быть пустым.";

        // Устанавливаем жесткий таймаут на операцию (например, 45 секунд), чтобы запрос не висел вечно
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

        var systemInstruction = mode.ToLower() switch
        {
            "elaborate" =>
                @"Ты — опытный копирайтер и редактор. Твоя задача — взять текст пользователя и расписать его намного подробнее. 
            Добавь деталей, разверни мысли, сделай текст более глубоким, структурированным и профессиональным. 
            Сохраняй изначальный смысл, но сделай подачу богаче.",

            _ => 
                @"Ты — мастер перефразирования. Твоя задача — переписать текст пользователя другими словами. 
            Сделай его более гладким, живым и приятным для чтения. Избегай тавтологии, улучшай стиль, 
            но строго сохраняй исходный смысл и посыл."
        };

        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = userText }
            },
            temperature = mode == "elaborate" ? 0.7 : 0.4, 
            max_tokens = 50 
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Url);
        
        // Подробный лог для дебага на сервере
        Console.WriteLine("\n==========================================================================================");
        Console.WriteLine($"[AI_MANAGER_DEBUG] Время: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"[AI_MANAGER_DEBUG] Статус ключа: {(string.IsNullOrEmpty(_apiKey) ? "ПУСТОЙ / NULL" : "ЗАПОЛНЕН")}");
        Console.WriteLine($"[AI_MANAGER_DEBUG] Длина ключа: {(_apiKey?.Length ?? 0)} симв. Значение: '{_apiKey}'");
        Console.WriteLine("==========================================================================================\n");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Add("HTTP-Referer", "https://project-domain.ru/"); 
        request.Content = JsonContent.Create(requestBody);

        try
        {
            // Передаем токен отмены cts.Token в SendAsync
            var response = await httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cts.Token);
                throw new Exception($"OpenRouter API Error: {response.StatusCode}. Details: {errorDetails}");
            }

            // Проверяем, что нам вернули именно JSON, а не HTML-страницу ошибки провайдера/хостинга
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != "application/json")
            {
                var rawContent = await response.Content.ReadAsStringAsync(cts.Token);
                throw new Exception($"Ожидался JSON, но сервер вернул {mediaType}. Ответ: {rawContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cts.Token);

            if (result.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentProperty))
            {
                var content = contentProperty.GetString();

                if (string.IsNullOrEmpty(content))
                    throw new Exception("OpenRouter returned success, but message content is empty.");

                return content.Trim();
            }

            throw new Exception("Failed to parse standard OpenAI/OpenRouter response structure.");
        }
        catch (TaskCanceledException)
        {
            throw new Exception("Запрос к OpenRouter был отменен по таймауту (сервер не ответил за 45 секунд).");
        }
    }
}