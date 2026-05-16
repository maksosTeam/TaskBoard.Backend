using AnalyticsService.Kafka.Requests;

using Confluent.Kafka;

using Kafka.Messaging.Contracts.Response;
using Kafka.Messaging.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public class GetItemByIdResponseConsumer
    : BackgroundService
{
    private readonly IConsumer<string,
        GetItemByIdResponse> consumer;

    private readonly string topic;

    public GetItemByIdResponseConsumer(
        IOptionsMonitor<KafkaSettings> settingsMonitor)
    {
        var settings =
            settingsMonitor.Get(
                "GetItemByIdResponse");

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
                    GetItemByIdResponse>(config)
                .SetValueDeserializer(
                    new KafkaValueDeserealizer<
                        GetItemByIdResponse>())
                .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); 

        consumer.Subscribe(topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result == null) continue;

                var response = result.Message.Value;

                if (PendingItemRequests.Requests.TryRemove(response.CorrelationId, out var tcs))
                {
                    tcs.SetResult(response);
                }
            }
            catch (OperationCanceledException)
            {
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