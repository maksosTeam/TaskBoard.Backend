using Confluent.Kafka;
using Kafka.Messaging.Contracts.Response;
using Kafka.Messaging.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public class GetItemsByProjectResponseConsumer
    : BackgroundService
{
    private readonly IConsumer<string,
        GetItemsByProjectResponse> consumer;

    private readonly string topic;

    public GetItemsByProjectResponseConsumer(
        IOptionsMonitor<KafkaSettings> settingsMonitor)
    {
        var settings =
            settingsMonitor.Get(
                "GetItemsByProjectResponse");

        topic = settings.Topic;

        var config = new ConsumerConfig
        {
            BootstrapServers =
                settings.BootstrapServers,

            GroupId =
                settings.GroupId,

            AutoOffsetReset =
                AutoOffsetReset.Earliest
        };

        consumer =
            new ConsumerBuilder<string,
                    GetItemsByProjectResponse>(config)
                .SetValueDeserializer(
                    new KafkaValueDeserealizer<
                        GetItemsByProjectResponse>())
                .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Важно: дать хосту запуститься, прежде чем уйти в бесконечный цикл
        await Task.Yield(); 

        consumer.Subscribe(topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Consume имеет перегрузку с CancellationToken, он разблокируется сам
                var result = consumer.Consume(stoppingToken);

                if (result == null) continue;

                var response = result.Message.Value;

                if (PendingRequests.Requests.TryRemove(response.CorrelationId, out var tcs))
                {
                    tcs.SetResult(response);
                }
            }
            catch (OperationCanceledException)
            {
                // Это нормальное исключение при остановке приложения
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении из Kafka: {ex.Message}");
            }
        }
    }

    public override Task StopAsync(
        CancellationToken cancellationToken)
    {
        consumer.Close();

        return base.StopAsync(cancellationToken);
    }
}