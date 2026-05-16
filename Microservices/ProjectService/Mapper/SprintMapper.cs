using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Models;

namespace ProjectService.Mapper;

public static class SprintMapper
{
    public static SprintEntity? ToEntity(SprintModel model)
    {
        if (model is null)
            return null;

        return new SprintEntity
        {
            Name = model.Name,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            BoardId = model.BoardId
        };
    }

    /// <summary>
    /// Синхронный маппинг спринта с передачей кэша пользователей в дочерний маппер задач
    /// </summary>
    public static SprintModel? ToModel(SprintEntity model, Dictionary<int, string> userNamesCache)
    {
        if (model is null)
            return null;

        // Маппим задачи внутри спринта, используя наш быстрый кэш
        var itemModels = model.Items?
            .Select(x => ItemMapper.ToModel(x, userNamesCache)!)
            .ToList() ?? new List<ItemModel>();

        return new SprintModel
        {
            Id = model.Id,
            Name = model.Name,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            BoardId = model.BoardId,
            Items = itemModels
        };
    }
}