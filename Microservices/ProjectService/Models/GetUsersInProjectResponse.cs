namespace ProjectService.Models;

public record GetUsersInProjectResponse(int UserId, string UserName, string ImagePath, string Email, string Role);