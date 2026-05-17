using Microsoft.AspNetCore.Mvc;
using ProjectService.BusinessLayer.Abstractions;

namespace ProjectService.Controllers;

[ApiController]
[Route("ai")]
public class AiController(IAiManager aiManager) : ControllerBase
{
    // <summary>
    /// Обрабатывает текст пользователя с помощью ИИ (перефразирование или детализация).
    /// </summary>
    /// <param name="userText">Исходный текст от пользователя.</param>
    /// <param name="mode">Режим работы: "paraphrase" (перефразировать) или "elaborate" (расписать подробнее).</param>
    [HttpPost("create")]
    public async Task<IActionResult> CreateMessage(string text, string mode = "paraphrase")
    {
        
        var result = await aiManager.ProcessUserText(text);
        return Ok(result);
    }
}