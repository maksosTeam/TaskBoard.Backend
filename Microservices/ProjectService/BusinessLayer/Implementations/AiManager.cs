using System.Net.Http.Headers;
using System.Text.Json;
using ProjectService.BusinessLayer.Abstractions;

namespace ProjectService.BusinessLayer.Implementations;

public class AiManager(HttpClient httpClient, IConfiguration configuration) : IAiManager
{
    private readonly string? _apiKey = configuration["apiKey"];
    private const string Url = "https://openrouter.ai/api/v1/chat/completions";
    private const string Model = "google/gemini-2.0-flash-001";

    // <summary>
    /// Обрабатывает текст пользователя с помощью ИИ (перефразирование или детализация).
    /// </summary>
    /// <param name="userText">Исходный текст от пользователя.</param>
    /// <param name="mode">Режим работы: "paraphrase" (перефразировать) или "elaborate" (расписать подробнее).</param>
    public async Task<string> ProcessUserText(string userText, string mode = "paraphrase")
    {
        if (string.IsNullOrWhiteSpace(userText)) return "Текст для обработки не может быть пустым.";

        // Формируем задачу для ИИ в зависимости от выбранного режима
        var systemInstruction = mode.ToLower() switch
        {
            "elaborate" =>
                @"Ты — опытный копирайтер и редактор. Твоя задача — взять текст пользователя и расписать его намного подробнее. 
            Добавь деталей, разверни мысли, сделай текст более глубоким, структурированным и профессиональным. 
            Сохраняй изначальный смысл, но сделай подачу богаче.",

            _ => // По умолчанию — paraphrase
                @"Ты — мастер перефразирования. Твоя задача — переписать текст пользователя другими словами. 
            Сделай его более гладким, живым и приятным для чтения. Избегай тавтологии, улучшай стиль, 
            но строго сохраняй исходный смысл и посыл."
        };

        // Настраиваем тело запроса. 
        // Теперь нам не нужен response_format (json), так как мы ждем обычный текст.
        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = userText }
            },
            temperature = mode == "elaborate" ? 0.7 : 0.4, // Для детализации даем чуть больше творческой свободы
            max_tokens = 1000 // Увеличили лимит, так как текст в режиме 'elaborate' может быть длинным
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Url);
        Console.WriteLine(_apiKey); //test
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Add("HTTP-Referer", "https://project-domain.ru/"); 
        request.Content = JsonContent.Create(requestBody);

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorDetails = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenRouter API Error: {response.StatusCode}. Details: {errorDetails}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Безопасно извлекаем текстовый ответ модели
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
}