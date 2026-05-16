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

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        consumer.Subscribe(topic);

        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result =
                        consumer.Consume(stoppingToken);

                    if (result == null)
                        continue;

                    var response =
                        result.Message.Value;

                    if (PendingItemRequests.Requests
                        .TryRemove(
                            response.CorrelationId,
                            out var tcs))
                    {
                        tcs.SetResult(response);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }, stoppingToken);
    }

    public override Task StopAsync(
        CancellationToken cancellationToken)
    {
        consumer.Close();

        return base.StopAsync(cancellationToken);
    }
}