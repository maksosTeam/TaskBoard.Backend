using SharedLibrary.Constants;
using SharedLibrary.Dapper.DapperRepositories;
using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.Entities.ProjectService;
using SharedLibrary.ProjectModels;

namespace ProjectService.Mapper
{
    public static class ProjectMapper
    {
        public static ProjectEntity ToEntity(ProjectModel projectModel)
        {
            return new ProjectEntity()
            {
                Name = projectModel.Name,
                Key = projectModel.Key,
                Description = projectModel.Description,
                ExpectedEndDate = projectModel.ExpectedEndDate,
                IsPrivate = projectModel.IsPrivate,
                Priority = projectModel.Priority,
                StartDate = projectModel.StartDate,
                UpdatedAt = projectModel.UpdateDate
            };
        }

        public static async Task<ProjectModel> ToModel(ProjectEntity projectModel, string? headUserName)
        {
            var project = new ProjectModel()
            {
                Id = projectModel.Id,
                Name = projectModel.Name,
                Key = projectModel.Key,
                Description = projectModel.Description,
                ExpectedEndDate = projectModel.ExpectedEndDate,
                IsPrivate = projectModel.IsPrivate,
                Priority = projectModel.Priority,
                StartDate = projectModel.StartDate,
                UpdateDate = projectModel.UpdatedAt,
                UserProjects = projectModel.UserProjects.Select(UserProjectMapper.ToModel).ToList()
            };
            
            project.SetHead(headUserName ?? "Не назначен");

            return project;
        }
    }
}
