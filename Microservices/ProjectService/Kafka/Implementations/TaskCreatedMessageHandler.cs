using Kafka.Messaging;
using Microsoft.AspNetCore.SignalR;
using ProjectService.Kafka.Abstractions;
using SharedLibrary.Constants;
using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.MailService;
using SharedLibrary.Models.KafkaModel;

namespace ProjectService.Kafka.Implementations
{
    public class TaskEventMessageHandler(
        ILogger<TaskEventMessageHandler> logger,
        IUserRepository userRepository,
        IEmailSender mailService,
        IHubContext<NotificationHub> hubContext 
    ) : IMessageHandler<TaskEventMessage>
    {
        public async Task HandleAsync(TaskEventMessage message, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Received task event: {message.EventType} — {message.UserItems}");

            var item = message.UserItems;
            if (item == null || !item.Any())
            {
                logger.LogWarning("No user items found in the message.");
                return;
            }

            var userIds = item.Select(x => x.UserId).Distinct().ToList();
            var emailToList = new List<string>();

            foreach (var id in userIds)
            {
                logger.LogInformation($"Processing user with ID: {id}");
                var user = await userRepository.GetUserAsync(id);
                if (user is null)
                {
                    logger.LogWarning($"User with ID {id} not found.");
                    continue;
                }
                emailToList.Add(user.Email);
            }
            
            var subject = GetSubject(message.EventType);
            var body = $"Task update:\n{string.Join("\n", message.Message)}";

            var notificationTasks = new List<Task>();

            if (emailToList.Any())
            {
                notificationTasks.Add(mailService.SendEmailAsync(subject, body, emailToList.ToArray()));
            }
            
            var stringUserIds = userIds.Select(id => id.ToString()).ToList();
            
            notificationTasks.Add(hubContext.Clients.Users(stringUserIds).SendAsync(
                "ReceiveTaskNotification", 
                new { eventType = message.EventType.ToString(), message = message.Message }, 
                cancellationToken
            ));
            
            await Task.WhenAll(notificationTasks);
        }

        private static string GetSubject(TaskEventType type) => type switch
        {
            TaskEventType.Updated => "Задача изменена!",
            TaskEventType.AddedUser => "Добавлен новый пользователь в задачу!"
        };
    }
}