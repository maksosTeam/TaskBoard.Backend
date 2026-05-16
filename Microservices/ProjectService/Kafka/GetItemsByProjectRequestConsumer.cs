using Kafka.Messaging.Settings;
using ProjectService.BusinessLayer.Abstractions;

using Confluent.Kafka;

using Kafka.Messaging;
using Kafka.Messaging.Contracts.Requests;
using Kafka.Messaging.Contracts.Response;

using Microsoft.Extensions.Options;

namespace ProjectService.Kafka;

public class GetItemsByProjectRequestConsumer : BackgroundService
{
    private readonly IConsumer<string, GetItemsByProjectRequest> consumer;

    private readonly IKafkaProducer<GetItemsByProjectResponse> producer;

    private readonly IServiceScopeFactory scopeFactory;

    private readonly string topic;

    public GetItemsByProjectRequestConsumer(
        IServiceScopeFactory scopeFactory,
        IKafkaProducer<GetItemsByProjectResponse> producer,
        IOptionsMonitor<KafkaSettings> settingsMonitor)
    {
        this.scopeFactory = scopeFactory;
        this.producer = producer;

        var settings =
            settingsMonitor.Get("GetItemsByProjectRequest");

        topic = settings.Topic;

        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        consumer = new ConsumerBuilder<string,
                GetItemsByProjectRequest>(config)
            .SetValueDeserializer(
                new KafkaValueDeserealizer<GetItemsByProjectRequest>())
            .Build();
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        consumer.Subscribe(topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result == null)
                    continue;

                using var scope = scopeFactory.CreateScope();

                var itemManager =
                    scope.ServiceProvider
                        .GetRequiredService<IItemManager>();

                var request = result.Message.Value;

                var items =
                    await itemManager.GetByProjectIdAsync(
                        request.ProjectId);

                await producer.ProduceAsync(
                    new GetItemsByProjectResponse
                    {
                        CorrelationId = request.CorrelationId,
                        Items = items
                    },
                    stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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