using System.ComponentModel;
using System.Reflection;
using SharedLibrary.Entities.ProjectService;

namespace SharedLibrary.Constants;

public enum StatusEnum
{
    [Description("В очереди")]
    Todo = 1,

    [Description("На исполнении")]
    InProgress = 2,

    [Description("На проверке")]
    Review = 3,

    [Description("Готово")]
    Done = 4,

    [Description("Отклонено")]
    Reject = 5,
}

public static class StatusEnumExtensions
{
    public static StatusEntity ToEntity(this StatusEnum status)
    {
        var field = status.GetType().GetField(status.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        var name = attribute?.Description ?? status.ToString();

        return status switch
        {
            StatusEnum.Todo => new StatusEntity { Name = name, Order = 0, IsDone = false, IsRejected = false },
            StatusEnum.InProgress => new StatusEntity { Name = name, Order = 1, IsDone = false, IsRejected = false },
            StatusEnum.Review => new StatusEntity { Name = name, Order = 2, IsDone = false, IsRejected = false },
            StatusEnum.Done => new StatusEntity { Name = name, Order = 3, IsDone = true, IsRejected = false },
            StatusEnum.Reject => new StatusEntity { Name = name, Order = 4, IsDone = false, IsRejected = true },
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}