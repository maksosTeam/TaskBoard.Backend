using ProjectService.BusinessLayer.Abstractions;
using ProjectService.DataLayer.Repositories.Abstractions;
using ProjectService.Exceptions;
using ProjectService.Mapper;
using ProjectService.Models;
using SharedLibrary.Auth;
using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Constants;
using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.Models.KafkaModel;
using SharedLibrary.Models;
using SharedLibrary.Entities;
using Kafka.Messaging.Services.Abstractions;
using ProjectService.Kafka.Implementations;
using SharedLibrary.Models.AnalyticModels;

namespace ProjectService.BusinessLayer.Implementations;

public class ItemManager(
    IItemRepository itemRepository,
    IValidateItemManager validatorManager,
    IItemBoardsRepository itemBoardsRepository,
    IProjectRepository projectRepository,
    ICommentRepository commentRepository,
    IAttachmentRepository attachmentRepository,
    IUserRepository userRepository,
    HttpClient httpClient,
    IAuth auth,
    IMessageHandler<TaskEventMessage> messageHandler) : IItemManager
{
    private async Task<ItemModel> EnrichItemAsync(ItemEntity entity)
    {
        var userIds = new HashSet<int>();
        if (entity.AuthorId.HasValue)
            userIds.Add(entity.AuthorId.Value);
        if (entity.UserItems != null)
        {
            foreach (var ui in entity.UserItems)
                userIds.Add(ui.UserId);
        }

        var cache = new Dictionary<int, string>();
        // Хранилище для отслеживания уже добавленных строк-значений
        var uniqueValues = new HashSet<string>();

        foreach (var id in userIds)
        {
            // Защита от дубликатов по ключу: если этот id уже обрабатывался, пропускаем
            if (cache.ContainsKey(id))
                continue;

            var user = await userRepository.GetUserAsync(id);
            if (user != null)
            {
                var value = $"{user.Username}@~{user.ImagePath}";

                if (uniqueValues.Add(value))
                {
                    cache[id] = value;
                }
            }
        }

        return ItemMapper.ToModel(entity, cache)!;
    }

    private async Task<IEnumerable<ItemModel>> EnrichItemsAsync(IEnumerable<ItemEntity> entities)
    {
        var entityList = entities.ToList();
        var userIds = new HashSet<int>();

        foreach (var entity in entityList)
        {
            if (entity.AuthorId.HasValue)
                userIds.Add(entity.AuthorId.Value);
            if (entity.UserItems != null)
            {
                foreach (var ui in entity.UserItems)
                    userIds.Add(ui.UserId);
            }
        }

        var cache = new Dictionary<int, string>();
        // Хранилище для отслеживания уже добавленных строк-значений
        var uniqueValues = new HashSet<string>();

        foreach (var id in userIds)
        {
            // Защита от дубликатов по ключу: если этот id уже обрабатывался, пропускаем
            if (cache.ContainsKey(id))
                continue;

            var user = await userRepository.GetUserAsync(id);
            if (user != null)
            {
                var value = $"{user.Username}@~{user.ImagePath}";

                if (uniqueValues.Add(value))
                {
                    cache[id] = value;
                }
            }
        }

        return entityList.Select(x => ItemMapper.ToModel(x, cache)!).ToList();
    }

    public async Task<int> CreateAsync(CreateItemModel createItemModel, CancellationToken token)
    {
        await validatorManager.ValidateCreateAsync(createItemModel);

        var currentUserId = auth.GetCurrentUserId();
        var project = await projectRepository.GetByBoardIdAsync(createItemModel.BoardId);

        var item = createItemModel.Item;
        var entity = ItemMapper.ItemToEntity(item);
        entity!.CreatedAt = DateTime.UtcNow;

        await itemRepository.CreateAsync(entity);

        entity.BusinessId = $"{project.Key}-ITEM-{entity.Id}";
        entity.AuthorId = currentUserId;

        await itemBoardsRepository.Create(
            new ItemBoardEntity
            {
                ItemId = entity.Id,
                BoardId = createItemModel.BoardId,
                StatusId = (int)entity.StatusId!
            }
        );

        entity = await itemRepository.GetByIdAsync(entity.Id);

        var model = new TaskHistoryModel
        {
            FieldName = "Новое задание",
            OldValue = "",
            NewValue = item.Title,
            ItemId = entity.Id,
            UserId = (int)currentUserId!,
            ChangedAt = DateTime.UtcNow
        };

        await httpClient.PostAsJsonAsync("create", model, cancellationToken: token);

        return entity.Id;
    }

    public async Task Delete(int id)
    {
        await itemRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<ItemModel>> GetAllItemsAsync()
    {
        var items = await itemRepository.GetItemsAsync();
        return await EnrichItemsAsync(items);
    }

    public async Task<ItemModel> GetByIdAsync(int? id)
    {
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        var entity = await itemRepository.GetByIdAsync((int)id);
        if (entity is null)
            throw new ItemNotFoundException();

        return await EnrichItemAsync(entity);
    }

    public async Task<ICollection<ItemModel>> GetByBoardIdAsync(int boardId)
    {
        var items = await itemRepository.GetItemsByBoardIdAsync(boardId);
        var models = await EnrichItemsAsync(items);
        return models.ToList();
    }

    public async Task<int> UpdateAsync(ItemModel item, CancellationToken token, string message, string oldValue, string newValue, string fieldName, int botId = -1,
        TaskEventType eventType = TaskEventType.Updated)
    {
        Console.WriteLine("\n==========================================================================================");
        Console.WriteLine($"[GITHUB] Время: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"[GITHUB] МЕНЯЕМ ЗАДАЧУ");
        Console.WriteLine("==========================================================================================\n");
        await validatorManager.ValidateItemModelAsync(item, botId);
        var entity = ItemMapper.ItemToEntity(item);
        entity!.Id = item.Id;

        var updatedAt = DateTime.UtcNow;
        entity.UpdatedAt = updatedAt;

        await itemRepository.UpdateAsync(entity);

        await messageHandler.HandleAsync(new TaskEventMessage
        {
            EventType = eventType,
            UserItems = item.UserItems,
            Message = message,
        }, token);
        
        var model = new TaskHistoryModel
        {
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ItemId = item.Id,
            UserId = botId != -1 ? botId : (int)auth.GetCurrentUserId()!,
            ChangedAt = updatedAt
        };

        await httpClient.PostAsJsonAsync("create", model, cancellationToken: token);
        return entity.Id;
    }

    public async Task<int> AddUserToItemAsync(int newUserId, int itemId, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(itemId);
        if (item is null)
            throw new ItemNotFoundException();

        await validatorManager.ValidateAddUserToItemAsync((int)item.ProjectId!, newUserId);

        var itemUserEntity = new UserItemEntity
        {
            ItemId = itemId,
            UserId = newUserId
        };

        // Красиво собираем старый и новый список ID через запятую для истории
        string oldUsersString = string.Join(", ", item.UserItems.Select(x => x.UserId));

        await itemRepository.AddUserToItemAsync(itemUserEntity);
        item.UserItems.Add(itemUserEntity);

        string newUsersString = string.Join(", ", item.UserItems.Select(x => x.UserId));

        var enrichedModel = await EnrichItemAsync(item);

        await UpdateAsync(
            enrichedModel,
            cancellationToken,
            $"В {item.Title} добавлен пользователь с айди {newUserId}",
            oldUsersString,
            newUsersString,
            "UserItems",
            -1,
            TaskEventType.AddedUser
        );

        return itemUserEntity.Id;
    }

    public async Task<ICollection<ItemModel>> GetArchievedItemsInProject(int projectId)
    {
        await validatorManager.ValidateUserInProjectAsync(projectId);
        var items = await itemRepository.GetItemsByProjectIdAsync(projectId);
        var models = await EnrichItemsAsync(items.Where(x => x.IsArchived));
        return models.ToList();
    }

    public async Task<ICollection<ItemModel>> GetArchievedItemsInBoard(int boardId)
    {
        var projectId = (await projectRepository.GetByBoardIdAsync(boardId)).Id;
        await validatorManager.ValidateUserInProjectAsync(projectId);
        var items = await itemRepository.GetItemsByBoardIdAsync(boardId);
        var models = await EnrichItemsAsync(items.Where(x => x.IsArchived));
        return models.ToList();
    }

    public async Task<ICollection<ItemModel>> GetBugsItemsInProject(int projectId)
    {
        await validatorManager.ValidateUserInProjectAsync(projectId);
        var items = await itemRepository.GetItemsByProjectIdAsync(projectId);
        var models = await EnrichItemsAsync(items.Where(x => x.ItemTypeId == ItemType.BUG));
        return models.ToList();
    }

    public async Task<ICollection<ItemModel>> GetBugsItemsInBoard(int boardId)
    {
        var projectId = (await projectRepository.GetByBoardIdAsync(boardId)).Id;
        await validatorManager.ValidateUserInProjectAsync(projectId);
        var items = await itemRepository.GetItemsByBoardIdAsync(boardId);
        var models = await EnrichItemsAsync(items.Where(x => x.ItemTypeId == ItemType.BUG));
        return models.ToList();
    }

    public async Task<ICollection<ItemModel>> GetByProjectIdAsync(int projectId)
    {
        await validatorManager.ValidateUserInProjectAsync(projectId);
        var items = await itemRepository.GetItemsByProjectIdAsync(projectId);
        var models = await EnrichItemsAsync(items);
        return models.ToList();
    }

    public async Task<ItemModel> GetByTitle(string title)
    {
        var entity = await itemRepository.GetByNameAsync(title);
        return await EnrichItemAsync(entity);
    }

    public async Task<ICollection<ItemModel>> GetCurrentUserItems()
    {
        var userId = auth.GetCurrentUserId();
        if (userId is null || userId == -1)
            throw new NotAuthorizedException();
        var items = await itemRepository.GetCurrentUserItemsAsync((int)userId);
        var models = await EnrichItemsAsync(items);
        return models.ToList();
    }

    public async Task<ICollection<ItemModel>> GetItemsByUserId(int userId, int projectId)
    {
        await validatorManager.ValidateAddUserToItemAsync(projectId, userId);
        var items = await itemRepository.GetItemsByUserIdAsync(userId, projectId);
        var models = await EnrichItemsAsync(items);
        return models.ToList();
    }

    public async Task<int> AddCommentToItemAsync(CommentModel commentModel, IFormFile? attachment)
    {
        var userId = auth.GetCurrentUserId();
        if (userId is null || userId == -1)
            throw new NotAuthorizedException();

        var item = await itemRepository.GetByIdAsync(commentModel.ItemId);
        if (item is null)
            throw new ItemNotFoundException();

        await validatorManager.ValidateUserInProjectAsync(item.ProjectId);
        var commentEntity = CommentMapper.ToEntity(commentModel);

        commentEntity!.AuthorId = (int)userId;

        await commentRepository.CreateAsync(commentEntity);

        if (attachment is not null)
        {
            var docPath = Environment.GetEnvironmentVariable("ATTACHMENT_STORAGE_PATH");

            if (string.IsNullOrEmpty(docPath))
                throw new ArgumentNullException(nameof(attachment), "Переменная окружения ATTACHMENT_STORAGE_PATH не задана");

            Directory.CreateDirectory(docPath);

            var uniqueFileName = $"{Guid.NewGuid()}_{attachment.FileName}";
            var filePath = Path.Combine(docPath, uniqueFileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await attachment.CopyToAsync(stream);
            }

            docPath = $"/attachments/{uniqueFileName}";

            var attachmentEntity = new AttachmentEntity
            {
                AuthorId = (int)userId,
                UploadedAt = DateTime.UtcNow,
                CommentId = commentEntity.Id,
                FilePath = docPath,
            };

            await attachmentRepository.CreateAsync(attachmentEntity);
        }

        var model = new TaskHistoryModel
        {
            FieldName = "Комментарий",
            OldValue = "",
            NewValue = commentModel.Text,
            ItemId = item.Id,
            UserId = (int)userId,
            ChangedAt = DateTime.UtcNow
        };

        await httpClient.PostAsJsonAsync("create", model);

        return commentEntity.Id;
    }

    public async Task<ICollection<CommentModel>> GetComments(int itemId)
    {
        var userId = auth.GetCurrentUserId();
        if (userId is null || userId == -1)
            throw new NotAuthorizedException();

        var item = await itemRepository.GetByIdAsync(itemId);
        if (item is null)
            throw new ItemNotFoundException();

        await validatorManager.ValidateUserInProjectAsync(item.ProjectId);

        var comments = commentRepository.GetByItemId(itemId);

        var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        var cache = new Dictionary<int, string>();
        foreach (var id in authorIds)
        {
            var user = await userRepository.GetUserAsync(id);
            if (user != null)
                cache[id] = user.Username;
        }

        var commentsModels = comments.Select(c => CommentMapper.ToModel(c, cache)).ToList();

        return commentsModels;
    }
}