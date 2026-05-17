using System.Security.Claims;
using System.Text;
using Kafka.Messaging;
using Kafka.Messaging.Services.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
            if (message == null)
                return;

            logger.LogInformation($"Received task event: {message.EventType}");

            var items = message.UserItems;
            if (items == null || !items.Any())
            {
                logger.LogWarning("No user items found in the message.");
                return;
            }

            // Получаем список уникальных ID пользователей
            var userIds = items.Select(x => x.UserId).Distinct().ToList();
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

            // 1. Отправка Email
            if (emailToList.Any())
            {
                notificationTasks.Add(mailService.SendEmailAsync(subject, body, emailToList.ToArray()));
            }

            // 2. Отправка через SignalR (вебсокеты)
            if (userIds.Any())
            {
                var stringUserIds = userIds.Select(id => id.ToString()).ToList();

                notificationTasks.Add(hubContext.Clients.Users(stringUserIds).SendAsync(
                    "ReceiveTaskNotification",
                    new { eventType = message.EventType.ToString(), message = message.Message },
                    cancellationToken
                ));
            }

            await Task.WhenAll(notificationTasks);
        }

        private static string GetSubject(TaskEventType type) => type switch
        {
            TaskEventType.Updated => "Задача изменена!",
            TaskEventType.AddedUser => "Добавлен новый пользователь в задачу!",
            _ => "Обновление в задаче проекта!"
        };
    }
}