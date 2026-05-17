using System.Text.RegularExpressions;
using Kafka.Messaging.Services.Abstractions;
using ProjectService.BusinessLayer.Abstractions;
using ProjectService.DataLayer.Repositories.Abstractions;
using ProjectService.Mapper;
using SharedLibrary.Constants;
using SharedLibrary.Models;
using SharedLibrary.Models.AnalyticModels;
using SharedLibrary.Models.KafkaModel;
using Sprache;

namespace ProjectService.Services;

public class GitHubWebhookService(
    IItemManager itemManager,
    IStatusManager statusManager,
    IItemRepository itemRepository,
    IMessageHandler<TaskEventMessage> messageHandler,
    ICommentRepository commentRepository,
    HttpClient httpClient
) : IGitHubWebhookService
{
    private static readonly Regex TaskKeyRegex = new(@"[a-zA-Z]+-\d+", RegexOptions.Compiled);

    public async Task<bool> ProcessPullRequestAsync(string action, string title, string htmlUrl, int botId,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("move task to review status");
        if (action != "opened" && action != "reopened") return false;

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(htmlUrl)) return false;

        var match = TaskKeyRegex.Match(title);
        if (!match.Success) return false;

        var taskKey = match.Value.ToUpper();
        var parse = int.TryParse(taskKey.Split('-').Last(), out var itemId);
        if (!parse) return false;

        var item = await itemManager.GetByIdAsync(itemId);
        Console.WriteLine(
            "\n==========================================================================================");
        Console.WriteLine($"[GITHUB] Время: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"[GITHUB] ПОЛУЧИЛИ ЗАДАЧУ {item}");
        Console.WriteLine(
            "==========================================================================================\n");

        var oldStatusName = item.Status?.Name ?? "Неизвестно";
        StatusModel status;
        if (item.Status?.Name != nameof(StatusEnum.Review))
        {
            Console.WriteLine(
                "\n==========================================================================================");
            Console.WriteLine($"[GITHUB] Время: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine("[GITHUB] ПЫТАЕМ ПОМЕНЯТЬ СТАТУС");
            Console.WriteLine(
                "==========================================================================================\n");

            if (!item.BoardId.HasValue) return false; // Защита от null для BoardId

            var statuses = await statusManager.GetByBoardIdAsync(item.BoardId.Value);

            status = statuses.FirstOrDefault(x => x.Name == "На проверке");

            Console.WriteLine(
                "\n==========================================================================================");
            Console.WriteLine($"[GITHUB] Время: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($";[GITHUB] ПОЛУЧИЛИ СТАРЫЙ СТАТУС {status?.Name}");
            Console.WriteLine(
                "==========================================================================================\n");

            if (status == null)
            {
                var id = await statusManager.CreateAsync(new StatusModel
                {
                    Name = "На проверке",
                    BoardId = item.BoardId.Value,
                    Order = 3,
                    IsDone = false,
                    IsRejected = false,
                });
                
                status = await statusManager.GetByIdAsync(id.Value);
            }

            item.StatusId = status.Id;

            await Update(item, cancellationToken,
                $"У задачи {item.Title} поменяли статус на {status.Name}",
                oldStatusName,
                status.Name,
                "Статус", botId);
        }

        var commentModel = new CommentModel
        {
            AuthorId = botId,
            ItemId = item.Id,
            Text = htmlUrl,
            CreatedAt = DateTime.UtcNow,
        };
        commentModel.SetName("PR");
        var commentEntity = CommentMapper.ToEntity(commentModel);
        var model = new TaskHistoryModel
        {
            FieldName = "Комментарий",
            OldValue = "",
            NewValue = commentModel.Text,
            ItemId = item.Id,
            UserId = botId,
            ChangedAt = DateTime.UtcNow
        };

        await httpClient.PostAsJsonAsync("create", model);
        await commentRepository.CreateAsync(commentEntity);
        return true;
    }

    private async Task Update(ItemModel item, CancellationToken token, string message, string oldValue,
        string newValue, string fieldName, int botId,
        TaskEventType eventType = TaskEventType.Updated)
    {
        Console.WriteLine("\n==========================================================================================");
        Console.WriteLine($"[GITHUB] Время: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"[GITHUB] МЕНЯЕМ ЗАДАЧУ");
        Console.WriteLine("==========================================================================================\n");
        
        var entity = ItemMapper.ItemToEntity(item);
        entity!.Id = item.Id;

        var updatedAt = DateTime.UtcNow;
        entity.UpdatedAt = updatedAt;

        await itemRepository.UpdateAsync(entity);

        await messageHandler.HandleAsync(new TaskEventMessage
        {
            EventType = eventType,
            UserItems = item.UserItems,
            Message = message
        }, token);

        var model = new TaskHistoryModel
        {
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ItemId = item.Id,
            UserId = botId,
            ChangedAt = updatedAt
        };

        await httpClient.PostAsJsonAsync("create", model, token);
    }
}