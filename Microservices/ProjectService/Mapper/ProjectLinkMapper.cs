using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Models;

namespace ProjectService.Mapper
{
    public static class ProjectLinkMapper
    {
        public static ProjectLinkEntity ToEntity(ProjectLinkModel model)
        {
            return new ProjectLinkEntity
            {
                ProjectId = model.ProjectId,
                Url = model.Url,
            };
        }

        public static ProjectLinkModel ToModel(ProjectLinkEntity entity, string? projectHeadUsername)
        {
            return new ProjectLinkModel
            {
                Id = entity.Id,
                ProjectId = entity.ProjectId,
                Project = entity.Project is null ? null : ProjectMapper.ToModel(entity.Project, projectHeadUsername),
                Url = entity.Url,
            };
        }
    }
}