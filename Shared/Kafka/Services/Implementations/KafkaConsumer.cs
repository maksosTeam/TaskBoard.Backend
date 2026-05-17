using Confluent.Kafka;
using Kafka.Messaging.Services.Abstractions;
using Kafka.Messaging.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Kafka.Messaging.Services.Implementations
{
    public class KafkaConsumer<TMessage> : BackgroundService
    {
        private readonly IOptionsMonitor<KafkaSettings> _optionsMonitor;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _configName;

        public KafkaConsumer(IOptionsMonitor<KafkaSettings> optionsMonitor, IServiceScopeFactory scopeFactory)
        {
            _optionsMonitor = optionsMonitor;
            _scopeFactory = scopeFactory;
            _configName = typeof(TMessage).Name;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken stoppingToken)
        {
            var settings = _optionsMonitor.Get(_configName);

            if (settings == null || string.IsNullOrEmpty(settings.BootstrapServers) || string.IsNullOrEmpty(settings.GroupId))
            {
                Trace.TraceError($"[Kafka: {_configName}] Критическая ошибка: Конфигурация не найдена или не заполнена! Консьюмер не запущен.");
                return;
            }

            var config = new ConsumerConfig
            {
                BootstrapServers = settings.BootstrapServers,
                GroupId = settings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest 
            };

            try
            {
                using var consumer = new ConsumerBuilder<string, TMessage>(config)
                    .SetValueDeserializer(new KafkaValueDeserealizer<TMessage>())
                    .Build();

                consumer.Subscribe(settings.Topic);
                Trace.TraceInformation($"[Kafka: {_configName}] Успешно подписались на топик: {settings.Topic}");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(stoppingToken);

                        if (result == null || result.Message.Value == null)
                            continue;

                        Console.WriteLine($"[Kafka: {_configName}] ПОЛУЧЕНО СООБЩЕНИЕ!");
                        Trace.TraceInformation($"[Kafka: {_configName}] Message received: " + result.Message.Value.ToString());

                        using var scope = _scopeFactory.CreateScope();
                        var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();

                        // Обрабатываем сообщение
                        await messageHandler.HandleAsync(result.Message.Value, stoppingToken);
                    }
                    catch (ConsumeException ex)
                    {
                        Trace.TraceError($"[Kafka: {_configName}] Ошибка чтения сообщения: {ex.Error.Reason}");
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError($"[Kafka: {_configName}] Ошибка во время обработки сообщения: {ex.Message}");
                    }
                }

                consumer.Close();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"[Kafka: {_configName}] Не удалось инициализировать или запустить консьюмер: {ex.Message}");
            }
        }
    }
}