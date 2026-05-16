using ProjectService.BusinessLayer.Abstractions;
using ProjectService.DataLayer.Repositories.Abstractions;
using ProjectService.Exceptions;
using ProjectService.Mapper;
using SharedLibrary.Constants;
using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.Entities.ProjectService;
using SharedLibrary.Models;

namespace ProjectService.BusinessLayer.Implementations;

public class ProjectLinkManager(
    IProjectLinkRepository projectLinkRepository,
    IProjectRepository projectRepository,
    IUserRepository userRepository)
    : IProjectLinkManager
{
    private async Task<string?> GetProjectHeadUsernameAsync(ProjectLinkEntity? entity)
    {
        var headProject = entity?.Project?.UserProjects?.FirstOrDefault(x => x.RoleId == DefaultRoles.CREATOR);
        if (headProject == null)
            return null;

        var user = await userRepository.GetUserAsync(headProject.UserId);
        return user?.Username;
    }

    public async Task<string> CreateAsync(int projectId)
    {
        var project = await projectRepository.GetByIdAsync(projectId);

        if (project is null)
            throw new ProjectNotFoundException();

        var url = Guid.NewGuid().ToString("N");
        var entity = new ProjectLinkEntity
        {
            ProjectId = projectId,
            Url = url
        };
        await projectLinkRepository.CreateAsync(entity);
        return url;
    }

    public async Task<ProjectLinkModel?> GetByIdAsync(int id)
    {
        var linkEntity = await projectLinkRepository.GetByIdAsync(id);
        if (linkEntity == null)
            return null;

        var headUsername = await GetProjectHeadUsernameAsync(linkEntity);
        return ProjectLinkMapper.ToModel(linkEntity, headUsername);
    }

    public async Task<ProjectLinkModel?> GetByLinkAsync(string link)
    {
        var linkEntity = await projectLinkRepository.GetByLinkAsync(link);
        if (linkEntity == null)
            return null;

        var headUsername = await GetProjectHeadUsernameAsync(linkEntity);
        return ProjectLinkMapper.ToModel(linkEntity, headUsername);
    }
}