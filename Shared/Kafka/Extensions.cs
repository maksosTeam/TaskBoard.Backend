using Kafka.Messaging.Services.Abstractions;
using Kafka.Messaging.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Kafka.Messaging.Services.Implementations;
using Microsoft.Extensions.Hosting;

namespace Kafka.Messaging
{
    public static class Extensions
    {
        public static void AddProducer<TMessage>(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            string configName = typeof(TMessage).Name;
            var section = configuration.GetSection($"Kafka:{configName}");

            serviceCollection.Configure<KafkaSettings>(configName, section);
            serviceCollection.AddSingleton<IKafkaProducer<TMessage>, KafkaProducer<TMessage>>();
        }

        public static void AddConsumer<TMessage, THandler>(this IServiceCollection serviceCollection, IConfiguration configuration)
            where THandler : class, IMessageHandler<TMessage>
        {
            string configName = typeof(TMessage).Name;
            var section = configuration.GetSection($"Kafka:{configName}");

            serviceCollection.Configure<KafkaSettings>(configName, section);

            serviceCollection.AddHostedService<KafkaConsumer<TMessage>>();
            serviceCollection.AddScoped<IMessageHandler<TMessage>, THandler>();
        }
    }
}