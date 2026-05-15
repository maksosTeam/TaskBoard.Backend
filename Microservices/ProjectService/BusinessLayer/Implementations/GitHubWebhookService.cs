using System.Text.RegularExpressions;
using ProjectService.BusinessLayer.Abstractions;

namespace ProjectService.Services;

public class GitHubWebhookService : IGitHubWebhookService
{
    // Здесь будет внедряться твой репозиторий Aggregate Root (например, ITaskRepository)
    // private readonly ITaskRepository _taskRepository;
    // private readonly IUnitOfWork _unitOfWork;
    // public GitHubWebhookService(ITaskRepository taskRepository, IUnitOfWork unitOfWork) ...

    private static readonly Regex TaskKeyRegex = new(@"[a-zA-Z]+-\d+", RegexOptions.Compiled);

    public async Task<bool> ProcessPullRequestAsync(string action, string title, string htmlUrl)
    {
        // 1. Проверяем доменное условие: интересны ли нам эти действия в рамках бизнес-логики
        if (action != "opened" && action != "reopened")
        {
            return false;
        }

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(htmlUrl))
        {
            return false;
        }

        // 2. Выделяем бизнес-идентификатор задачи
        var match = TaskKeyRegex.Match(title);
        if (!match.Success)
        {
            return false;
        }

        var taskKey = match.Value.ToUpper();

        // 3. Чистый DDD сценарий:
        // var task = await _taskRepository.GetByKeyAsync(taskKey);
        // if (task == null) return false;
        //
        // task.LinkPullRequest(htmlUrl); // Внутренний метод сущности (Domain Event внутри, если надо)
        //
        // await _taskRepository.UpdateAsync(task);
        // await _unitOfWork.SaveChangesAsync();

        return true;
    }
}