using Confluent.Kafka;
using Kafka.Messaging;
using Kafka.Messaging.Settings;
using Microsoft.Extensions.Options;

public class KafkaProducer<TMessage> : IKafkaProducer<TMessage>
{
    private readonly IProducer<string, TMessage> producer;
    private readonly string topic;

    public KafkaProducer(IOptionsMonitor<KafkaSettings> kafkaSettingsOptions)
    {
        var settingsName = typeof(TMessage).Name;
        var settings = kafkaSettingsOptions.Get(settingsName);

        var config = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers ?? "kafka:29092"
        };

        producer = new ProducerBuilder<string, TMessage>(config)
            .SetValueSerializer(new KafkaJsonSerializer<TMessage>())
            .Build();

        topic = settings.Topic; 
    }

    public async Task ProduceAsync(TMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(topic))
        {
            throw new ArgumentNullException(nameof(topic), 
                $"Топик для сообщения {typeof(TMessage).Name} не настроен в конфигурации!");
        }

        await producer.ProduceAsync(
            topic,
            new Message<string, TMessage>
            {
                Key = "1",
                Value = message
            },
            cancellationToken);
    }

    public void Dispose()
    {
        producer.Dispose();
        GC.SuppressFinalize(this);
    }
}