using System.Text.RegularExpressions;
using ProjectService.BusinessLayer.Abstractions;
using SharedLibrary.Constants;
using SharedLibrary.Models;

namespace ProjectService.Services;

public class GitHubWebhookService(IItemManager itemManager, IStatusManager statusManager) : IGitHubWebhookService
{
    private static readonly Regex TaskKeyRegex = new(@"[a-zA-Z]+-\d+", RegexOptions.Compiled);

    public async Task<bool> ProcessPullRequestAsync(string action, string title, string htmlUrl, CancellationToken cancellationToken = default)
    {
        if (action != "opened" && action != "reopened")
        {
            return false;
        }

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(htmlUrl))
        {
            return false;
        }

        var match = TaskKeyRegex.Match(title);
        if (!match.Success)
        {
            return false;
        }

        var taskKey = match.Value.ToUpper();
        var parse = int.TryParse(taskKey.Split('-').Last(), out var itemId);
        if (parse == false)
        {
            return false;
        }
        
        var item = await itemManager.GetByIdAsync(itemId);
        var oldStatusName = item.Status.Name;
        StatusModel status;
        if (item.Status?.Name != nameof(StatusEnum.Review))
        {
            status = (await statusManager.GetByBoardIdAsync(item.BoardId.Value))
                .Where(x => x.Name == nameof(StatusEnum.Review))
                .FirstOrDefault();

            item.StatusId = status?.Id;
            await itemManager.UpdateAsync(item, cancellationToken, 
                $"У задачи {item.Title} поменяли статус на {status.Name}", oldStatusName, 
                status.Name, "Статус");
        }
       
        
        item.MergeLink = htmlUrl;
       
        await itemManager.UpdateAsync(item, cancellationToken, 
            $"У задачи {item.Title} добавилась ссылка на ПР {htmlUrl}", string.Empty, 
            htmlUrl, "MergeLink");
        
        return true;
    }
}