using SharedLibrary.Constants;
using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Models;

namespace ProjectService.Mapper;

public static class ItemMapper
{
    public static ItemEntity? ItemToEntity(ItemModel item)
    {
        if (item is null)
            return null;

        return new ItemEntity
        {
            BusinessId = item.BusinessId,
            ParentId = item.ParentId,
            ProjectId = item.ProjectId,
            ProjectItemNumber = item.ProjectItemNumber,
            Title = item.Title,
            Description = item.Description,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            StartDate = item.StartDate,
            ExpectedEndDate = item.ExpectedEndDate,
            Priority = item.Priority,
            ItemTypeId = item.ItemTypeId,
            StatusId = (int)item.StatusId!,
            IsArchived = item.IsArchived,
            UserItems = item.UserItems != null
                ? item.UserItems.Select(UserItemMapper.ToEntity).ToList()
                : new List<UserItemEntity>(),
            MergeLink = item.MergeLink
        };
    }

    /// <summary>
    /// Синхронный маппинг в модель с использованием переданного кэша пользователей (решает проблему N+1)
    /// </summary>
    public static ItemModel? ToModel(ItemEntity? item, Dictionary<int, string> userNamesCache)
    {
        if (item is null)
            return null;

        var model = new ItemModel
        {
            Id = item.Id,
            BusinessId = item.BusinessId,
            ParentId = item.ParentId,
            ProjectId = item.ProjectId,
            ProjectItemNumber = item.ProjectItemNumber,
            Title = item.Title,
            Description = item.Description,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            StartDate = item.StartDate,
            ExpectedEndDate = item.ExpectedEndDate,
            Priority = item.Priority,
            ItemTypeId = item.ItemTypeId,
            StatusId = item.StatusId,
            IsArchived = item.IsArchived,
            Status = StatusMapper.ToModel(item.Status),
            UserItems = item.UserItems?.Select(UserItemMapper.ToModel).ToList() ?? new List<UserItemModel>(),
            MergeLink = item.MergeLink
        };

        if (item.ItemsBoards != null && item.ItemsBoards.Count > 0)
        {
            var boardBound = item.ItemsBoards.FirstOrDefault(x => x.ItemId == model.Id);
            if (boardBound != null)
            {
                model.SetBoardId(boardBound.BoardId);
            }
        }

        if (item.UserItems != null)
        {
            foreach (var userItem in item.UserItems)
            {
                if (userNamesCache.TryGetValue(userItem.UserId, out var username))
                {
                    model.AddContributor(username);
                }
                else
                {
                    model.AddContributor($"User_ID_{userItem.UserId}");
                }
            }
        }

        if (item.AuthorId.HasValue && userNamesCache.TryGetValue(item.AuthorId.Value, out var authorName))
        {
            model.SetAuthor(authorName);
        }
        else
        {
            model.SetAuthor(item.AuthorId?.ToString() ?? "Unknown");
        }

        return model;
    }
}