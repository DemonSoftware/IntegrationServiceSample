using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ProcessingService.Configuration;
using RabbitMQ.Client.Events;

namespace ProcessingService.Messaging
{
    public interface IRabbitMqService : IDisposable
    {
        Task<IConnection> GetConnectionAsync();
        Task<IChannel> GetChannelAsync();
        Task PublishResponseAsync(string message, string replyTo);
        Task<bool> CheckConnectionAsync();
    }

    public class RabbitMqService(IOptions<RabbitMqSettings> settings, ILogger<RabbitMqService> logger)
        : IRabbitMqService
    {
        private readonly RabbitMqSettings _settings = settings.Value;
        private IConnection _connection;
        private IChannel _channel;
        private bool _disposed;

        public async Task<IConnection> GetConnectionAsync()
        {
            if (_connection != null && _connection.IsOpen)
                return _connection;

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_settings.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = await factory.CreateConnectionAsync();
                
                // Add event handler for connection shutdown using async pattern
                _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;

                logger.LogInformation("RabbitMQ connection created successfully");
                return _connection;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating RabbitMQ connection");
                throw;
            }
        }

        public async Task<IChannel> GetChannelAsync()
        {
            if (_channel != null && _channel.IsOpen)
                return _channel;

            try
            {
                var connection = await GetConnectionAsync();
                _channel = await connection.CreateChannelAsync();

                // Declare processing queue
                await _channel.QueueDeclareAsync(
                    queue: _settings.ProcessingQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                // Declare response queue if specified
                if (!string.IsNullOrEmpty(_settings.ResponseQueueName))
                {
                    await _channel.QueueDeclareAsync(
                        queue: _settings.ResponseQueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false);
                }

                logger.LogInformation("RabbitMQ channel created successfully");
                return _channel;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating RabbitMQ channel");
                throw;
            }
        }

        public async Task PublishResponseAsync(string message, string replyTo)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqService));

            try
            {
                var channel = await GetChannelAsync();

                var body = Encoding.UTF8.GetBytes(message);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json"
                };

                // Publish to the reply-to queue if provided, otherwise to the response queue
                var routingKey = !string.IsNullOrEmpty(replyTo) 
                    ? replyTo 
                    : _settings.ResponseQueueName;

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                logger.LogInformation("Response published successfully to queue {Queue}", routingKey);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing response");
                throw;
            }
        }

        public async Task<bool> CheckConnectionAsync()
        {
            if (_disposed) return false;

            try
            {
                var connection = await GetConnectionAsync();
                var channel = await GetChannelAsync();

                return connection.IsOpen && channel.IsOpen;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking RabbitMQ connection");
                return false;
            }
        }

        private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
        {
            logger.LogWarning("RabbitMQ connection shutdown: {0}", e.ReplyText);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _channel?.CloseAsync().GetAwaiter().GetResult();
                _channel?.Dispose();
                _channel = null;

                _connection?.CloseAsync().GetAwaiter().GetResult();
                _connection?.Dispose();
                _connection = null;
            }

            _disposed = true;
        }
    }
}