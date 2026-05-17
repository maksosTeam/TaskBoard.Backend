using Kafka.Messaging.Services.Abstractions;
using Kafka.Messaging.Services.Implementations;
using Kafka.Messaging.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kafka.Messaging
{
    public static class Extensions
    {
        public static IServiceCollection AddProducer<TMessage>(this IServiceCollection services, IConfiguration configuration)
        {
            var configName = GetConfigName(typeof(TMessage));

            // ДОБАВЛЕНО: $"Kafka:{configName}" - потому что в .env у тебя префикс KAFKA__
            services.Configure<KafkaSettings>(configName, configuration.GetSection($"Kafka:{configName}"));

            services.AddSingleton<IKafkaProducer<TMessage>, KafkaProducer<TMessage>>();
            return services;
        }

        public static void AddConsumer<TMessage, THandler>(this IServiceCollection serviceCollection, IConfiguration configuration)
            where THandler : class, IMessageHandler<TMessage>
        {
            string configName = GetConfigName(typeof(TMessage));

            var section = configuration.GetSection($"Kafka:{configName}");

            serviceCollection.Configure<KafkaSettings>(configName, section);
            serviceCollection.AddHostedService<KafkaConsumer<TMessage>>();
            serviceCollection.AddScoped<IMessageHandler<TMessage>, THandler>();
        }

        public static string GetConfigName(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            var baseName = type.Name.Split('`')[0];
            var genericArgs = string.Join("_", type.GetGenericArguments().Select(t => t.Name.Replace("`", "")));
            return $"{baseName}_{genericArgs}";
        }
    }
}