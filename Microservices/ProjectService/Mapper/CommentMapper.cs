using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Models;

namespace ProjectService.Mapper;

public static class CommentMapper
{
    public static CommentEntity? ToEntity(CommentModel? model)
    {
        if (model is null)
            return null;

        return new CommentEntity
        {
            AuthorId = model.AuthorId,
            ItemId = model.ItemId,
            Text = model.Text,
            CreatedAt = model.CreatedAt
        };
    }

    /// <summary>
    /// Синхронный маппинг комментария с использованием готового кэша имен пользователей
    /// </summary>
    public static CommentModel? ToModel(CommentEntity? entity, Dictionary<int, string> userNamesCache)
    {
        if (entity is null)
            return null;

        var model = new CommentModel
        {
            Id = entity.Id,
            AuthorId = entity.AuthorId,
            ItemId = entity.ItemId,
            Text = entity.Text,
            CreatedAt = entity.CreatedAt,
            Attachments = entity.Attachments?.Select(AttachmentMapper.ToModel).ToList() ?? new List<AttachmentModel>()
        };

        if (entity.AuthorId != null)
        {
            if (userNamesCache.TryGetValue(entity.AuthorId, out var username))
            {
                model.SetName(username);
            }
            else
            {
                model.SetName($"{entity.AuthorId}");
            }
        }

        return model;
    }
}