using Confluent.Kafka;

using Kafka.Messaging;
using Kafka.Messaging.Contracts.Requests;
using Kafka.Messaging.Contracts.Response;
using Kafka.Messaging.Settings;

using Microsoft.Extensions.Options;

using ProjectService.BusinessLayer.Abstractions;

namespace ProjectsService.Kafka.Consumers;

public class GetProjectByIdRequestConsumer
    : BackgroundService
{
    private readonly IConsumer<string,
        GetProjectByIdRequest> consumer;

    private readonly IKafkaProducer<
        GetProjectByIdResponse> producer;

    private readonly IServiceScopeFactory scopeFactory;

    private readonly string topic;

    public GetProjectByIdRequestConsumer(
        IServiceScopeFactory scopeFactory,
        IKafkaProducer<GetProjectByIdResponse> producer,
        IOptionsMonitor<KafkaSettings> settingsMonitor)
    {
        this.scopeFactory = scopeFactory;
        this.producer = producer;

        var settings =
            settingsMonitor.Get("GetProjectByIdRequest");

        topic = settings.Topic;

        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        consumer =
            new ConsumerBuilder<string,
                    GetProjectByIdRequest>(config)
                .SetValueDeserializer(
                    new KafkaValueDeserealizer<
                        GetProjectByIdRequest>())
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
                var result =
                    consumer.Consume(stoppingToken);

                if (result == null)
                    continue;

                using var scope =
                    scopeFactory.CreateScope();

                var projectManager =
                    scope.ServiceProvider
                        .GetRequiredService<IProjectManager>();

                var request =
                    result.Message.Value;

                var project =
                    await projectManager.GetByIdAsync(
                        request.ProjectId);

                await producer.ProduceAsync(
                    new GetProjectByIdResponse
                    {
                        CorrelationId = request.CorrelationId,
                        Project = project
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