using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Models;
using SharedLibrary.ProjectModels;

namespace ProjectService.Mapper
{
    public static class BoardMapper
    {
        public static BoardEntity ToEntity(BoardModel model)
        {
            return new BoardEntity
            {
                Name = model.Name,
                Description = model.Description,
                CreatedAt = model.CreatedAt,
                ProjectId = model.ProjectId,
            };
        }

        public static BoardModel ToModel(BoardEntity entity, string? projectHeadUsername)
        {
            var model = new BoardModel
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                CreatedAt = entity.CreatedAt,
                ProjectId = entity.ProjectId,
                Project = entity.Project is null ? null : ProjectMapper.ToModel(entity.Project, projectHeadUsername),
                Statuses = entity.Statuses.Select(StatusMapper.ToModel).ToList()
            };

            model.SetItemsCount(entity.ItemsBoards.Count);

            if (entity.Project != null)
            {
                model.SetProjectName(entity.Project.Name);
            }

            return model;
        }
    }
}