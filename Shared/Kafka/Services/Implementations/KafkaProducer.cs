using Confluent.Kafka;
using Kafka.Messaging.Services.Abstractions;
using Kafka.Messaging.Settings;
using Microsoft.Extensions.Options;

namespace Kafka.Messaging.Services.Implementations
{
    public class KafkaProducer<TMessage> : IKafkaProducer<TMessage>
    {
        private readonly IProducer<string, TMessage> producer;
        private readonly string topic;

        public KafkaProducer(IOptionsMonitor<KafkaSettings> optionsMonitor)
        {
            var configName = typeof(TMessage).Name;
            var settings = optionsMonitor.Get(configName);

            var config = new ProducerConfig
            {
                BootstrapServers = settings?.BootstrapServers ?? throw new ArgumentNullException($"BootstrapServers для {configName} не настроен.")
            };

            producer = new ProducerBuilder<string, TMessage>(config)
                .SetValueSerializer(new KafkaJsonSerializer<TMessage>())
                .Build();

            topic = settings.Topic;
        }

        public async Task ProduceAsync(TMessage message, CancellationToken cancellationToken)
        {
            await producer.ProduceAsync(
                topic,
                new Message<string, TMessage> { Key = Guid.NewGuid().ToString(), Value = message },
                cancellationToken);
        }

        public void Dispose()
        {
            producer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}