using Confluent.Kafka;

using Kafka.Messaging;
using Kafka.Messaging.Contracts.Requests;
using Kafka.Messaging.Contracts.Response;
using Kafka.Messaging.Settings;

using Microsoft.Extensions.Options;

using ProjectService.BusinessLayer.Abstractions;

public class GetItemByIdRequestConsumer : BackgroundService
{
    private readonly IConsumer<string, GetItemByIdRequest> consumer;

    private readonly IKafkaProducer<GetItemByIdResponse> producer;

    private readonly IServiceScopeFactory scopeFactory;

    private readonly string topic;

    public GetItemByIdRequestConsumer(
        IServiceScopeFactory scopeFactory,
        IKafkaProducer<GetItemByIdResponse> producer,
        IOptionsMonitor<KafkaSettings> settingsMonitor)
    {
        this.scopeFactory = scopeFactory;
        this.producer = producer;

        var settings =
            settingsMonitor.Get("GetItemByIdRequest");

        topic = settings.Topic;

        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        consumer = new ConsumerBuilder<string,
                GetItemByIdRequest>(config)
            .SetValueDeserializer(
                new KafkaValueDeserealizer<GetItemByIdRequest>())
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

                var item =
                    await itemManager.GetByIdAsync(
                        request.ItemId);

                await producer.ProduceAsync(
                    new GetItemByIdResponse
                    {
                        CorrelationId =
                            request.CorrelationId,

                        Item = item
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