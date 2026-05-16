using ProjectService.BusinessLayer.Abstractions;
using ProjectService.DataLayer.Repositories.Abstractions;
using ProjectService.Exceptions;
using ProjectService.Mapper;
using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Models;

namespace ProjectService.BusinessLayer.Implementations;

public class SprintManager : ISprintManager
{
    private readonly ISprintRepository sprintRepository;
    private readonly IBoardRepository boardRepository;
    private readonly IItemRepository itemRepository;
    private readonly IValidateSprintManager _validatorManager;
    private readonly IUserRepository userRepository;

    public SprintManager(
        ISprintRepository sprintRepository,
        IBoardRepository boardRepository,
        IItemRepository itemRepository,
        IValidateSprintManager validatorManager,
        IUserRepository userRepository)
    {
        this.sprintRepository = sprintRepository;
        this.boardRepository = boardRepository;
        this.itemRepository = itemRepository;
        this._validatorManager = validatorManager;
        this.userRepository = userRepository;
    }

    #region Вспомогательные методы пакетной загрузки пользователей (Победа над N+1)

    private async Task<SprintModel> EnrichSprintAsync(SprintEntity entity)
    {
        if (entity is null)
            return null!;

        var userIds = new HashSet<int>();
        if (entity.Items != null)
        {
            foreach (var item in entity.Items)
            {
                if (item.AuthorId.HasValue)
                    userIds.Add(item.AuthorId.Value);
                if (item.UserItems != null)
                {
                    foreach (var ui in item.UserItems)
                        userIds.Add(ui.UserId);
                }
            }
        }

        var cache = new Dictionary<int, string>();
        foreach (var id in userIds)
        {
            var user = await userRepository.GetUserAsync(id);
            if (user != null)
                cache[id] = user.Username;
        }

        return SprintMapper.ToModel(entity, cache)!;
    }

    private async Task<IEnumerable<SprintModel>> EnrichSprintsAsync(IEnumerable<SprintEntity> entities)
    {
        var entityList = entities.ToList();
        var userIds = new HashSet<int>();

        foreach (var sprint in entityList)
        {
            if (sprint.Items == null)
                continue;
            foreach (var item in sprint.Items)
            {
                if (item.AuthorId.HasValue)
                    userIds.Add(item.AuthorId.Value);
                if (item.UserItems != null)
                {
                    foreach (var ui in item.UserItems)
                        userIds.Add(ui.UserId);
                }
            }
        }

        var cache = new Dictionary<int, string>();
        foreach (var id in userIds)
        {
            var user = await userRepository.GetUserAsync(id);
            if (user != null)
                cache[id] = user.Username;
        }

        return entityList.Select(x => SprintMapper.ToModel(x, cache)!).ToList();
    }

    #endregion

    public async Task AddItem(int sprintId, int itemId)
    {
        var existingSprint = await sprintRepository.GetByIdAsync(sprintId);

        if (existingSprint is null)
            throw new SprintNotFoundException();

        var existingItem = await itemRepository.GetByIdAsync(itemId);

        if (existingItem is null)
            throw new ItemNotFoundException();

        if (existingItem.ProjectId != existingSprint.Board.ProjectId)
            throw new DifferentAreaException();

        await _validatorManager.ValidateUserInProjectAsync(existingSprint.Board.ProjectId);

        await sprintRepository.AddItem(sprintId, itemId);
    }

    public async Task<int?> CreateAsync(SprintModel sprintModel)
    {
        var existingBoard = await boardRepository.GetByIdAsync(sprintModel.BoardId);

        if (existingBoard is null)
            throw new BoardNotFoundException();

        await _validatorManager.ValidateUserInProjectAsync(existingBoard.ProjectId);

        var sprintEntity = SprintMapper.ToEntity(sprintModel);

        await sprintRepository.CreateAsync(sprintEntity);

        return sprintEntity.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var existingSprint = await sprintRepository.GetByIdAsync(id);

        if (existingSprint is null)
            throw new SprintNotFoundException();

        await _validatorManager.ValidateUserInProjectAsync(existingSprint.Board.ProjectId);

        await sprintRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<SprintModel>> GetByBoardIdAsync(int boardId)
    {
        var existingBoard = await boardRepository.GetByIdAsync(boardId);

        if (existingBoard is null)
            throw new BoardNotFoundException();

        await _validatorManager.ValidateUserInProjectAsync(existingBoard.ProjectId);

        var entities = await sprintRepository.GetByBoardId(boardId);

        return await EnrichSprintsAsync(entities);
    }

    public async Task<SprintModel> GetByIdAsync(int id)
    {
        var existingSprint = await sprintRepository.GetByIdAsync(id);

        if (existingSprint is null)
            throw new SprintNotFoundException();

        await _validatorManager.ValidateUserInProjectAsync(existingSprint.Board.ProjectId);

        return await EnrichSprintAsync(existingSprint);
    }

    public async Task<int?> UpdateAsync(SprintModel sprintModel)
    {
        var existingSprint = await sprintRepository.GetByIdAsync(sprintModel.Id);

        if (existingSprint is null)
            throw new SprintNotFoundException();

        await _validatorManager.ValidateUserInProjectAsync(existingSprint.Board.ProjectId);

        var entity = SprintMapper.ToEntity(sprintModel);

        await sprintRepository.UpdateAsync(entity);

        return entity.Id;
    }
}