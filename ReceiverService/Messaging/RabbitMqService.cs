using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReceiverService.Configuration;
using System.Text.Json;

namespace ReceiverService.Messaging
{
    public interface IRabbitMqService : IDisposable
    {
        Task PublishAsync(string message);
        Task<string> PublishAndWaitResponseAsync(string message, TimeSpan? timeout = null);
        Task<bool> CheckConnectionAsync();
    }

    public class RabbitMqService(IOptions<RabbitMqSettings> settings, ILogger<RabbitMqService> logger)
        : IRabbitMqService
    {
        private readonly RabbitMqSettings _settings = settings.Value;
        private bool _disposed;

        // Simple publish without waiting for response
        public async Task PublishAsync(string message)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqService));
            
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_settings.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };
                
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();
                
                await channel.QueueDeclareAsync(
                    queue: _settings.ProcessingQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                
                var body = Encoding.UTF8.GetBytes(message);
                
                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json"
                };
                
                await channel.BasicPublishAsync(
                    exchange: _settings.ExchangeName,
                    routingKey: _settings.ProcessingQueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);
                
                logger.LogInformation("Message published successfully to queue {Queue}", _settings.ProcessingQueueName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing message to queue {Queue}", _settings.ProcessingQueueName);
                throw;
            }
        }

        // Publish message and wait for a response from the consumer
        public async Task<string> PublishAndWaitResponseAsync(string message, TimeSpan? timeout = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqService));
            
            timeout ??= TimeSpan.FromSeconds(30); // Default timeout of 30 seconds
            
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_settings.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };
                
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();
                
                // Ensure main queue exists
                await channel.QueueDeclareAsync(
                    queue: _settings.ProcessingQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                
                // Create a temporary reply queue
                var replyQueueName = (await channel.QueueDeclareAsync(
                    queue: "", // Empty name creates a unique, auto-delete queue
                    durable: false,
                    exclusive: true,
                    autoDelete: true,
                    arguments: null)).QueueName;
                
                logger.LogInformation("Created reply queue: {ReplyQueue}", replyQueueName);
                
                // Create a TaskCompletionSource to wait for the response
                var tcs = new TaskCompletionSource<string>();
                
                // Create a consumer for the reply queue
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (model, ea) =>
                {
                    var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                    logger.LogInformation("Received response: {Response}", response);
                    tcs.TrySetResult(response);
                    return Task.CompletedTask;
                };
                
                // Start consuming from the reply queue
                await channel.BasicConsumeAsync(
                    queue: replyQueueName,
                    autoAck: true,
                    consumer: consumer);
                
                // Create message properties with the reply-to set to our temporary queue
                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json",
                    ReplyTo = replyQueueName // This tells the consumer where to send the response
                };
                
                // Convert message to bytes
                var body = Encoding.UTF8.GetBytes(message);
                
                // Publish the message
                await channel.BasicPublishAsync(
                    exchange: _settings.ExchangeName,
                    routingKey: _settings.ProcessingQueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);
                
                logger.LogInformation("Message published successfully to queue {Queue} with reply-to {ReplyQueue}", 
                    _settings.ProcessingQueueName, replyQueueName);
                
                // Wait for the response with a timeout
                using var cts = new CancellationTokenSource(timeout.Value);
                
                try
                {
                    var response = await tcs.Task.WaitAsync(cts.Token);
                    logger.LogInformation("Got response for message");
                    return response;
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Timeout waiting for response from consumer");
                    throw new TimeoutException($"No response received within {timeout.Value.TotalSeconds} seconds");
                }
            }
            catch (Exception ex) when (!(ex is TimeoutException))
            {
                logger.LogError(ex, "Error in PublishAndWaitResponseAsync");
                throw;
            }
        }

        public async Task<bool> CheckConnectionAsync()
        {
            if (_disposed) return false;
            
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_settings.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };
                
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();
                
                await channel.QueueDeclarePassiveAsync(_settings.ProcessingQueueName);
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking RabbitMQ connection");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            _disposed = true;
        }
    }
}