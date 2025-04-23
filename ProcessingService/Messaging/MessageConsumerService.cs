using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessingService.Configuration;
using ProcessingService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProcessingService.Messaging
{
    public class MessageConsumerService(IRabbitMqService rabbitMqService,
            IMessageProcessor messageProcessor,
            IOptions<RabbitMqSettings> settings,
            ILogger<MessageConsumerService> logger)
        : BackgroundService
    {
        private readonly IRabbitMqService _rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
        private readonly IMessageProcessor _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
        private readonly RabbitMqSettings _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        private readonly ILogger<MessageConsumerService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private IConnection _connection;
        private IChannel _channel;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Message Consumer Service is starting");

            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Message Consumer Service is stopping");
            });

            await InitializeRabbitMQAsync(stoppingToken);

            // Keep the service running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check if the connection is still open
                    if (_connection == null || !_connection.IsOpen || _channel == null || !_channel.IsOpen)
                    {
                        _logger.LogWarning("RabbitMQ connection lost. Reconnecting...");
                        await InitializeRabbitMQAsync(stoppingToken);
                    }

                    // Sleep a bit between checks to prevent a tight loop
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in message consumer service");
                    await Task.Delay(5000, stoppingToken); // Wait before retry
                }
            }

            // Clean up resources
            await CleanupRabbitMQAsync();
        }

        private async Task InitializeRabbitMQAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Get connection and channel
                _connection = await _rabbitMqService.GetConnectionAsync();
                _channel = await _rabbitMqService.GetChannelAsync();

                // Configure QoS (prefetch)
                await _channel.BasicQosAsync(0, 1, false);

                // Create a consumer
                var consumer = new AsyncEventingBasicConsumer(_channel);
                
                // Define the message handling function
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    string message = null;
                    
                    try
                    {
                        var body = ea.Body.ToArray();
                        message = Encoding.UTF8.GetString(body);
                        
                        _logger.LogInformation("Received message: {MessageLength} bytes", body.Length);
                        _logger.LogDebug("Message content: {Message}", message);
                        
                        // Get the reply-to queue if present
                        var replyTo = ea.BasicProperties.ReplyTo;
                        
                        // Process the message
                        var result = await _messageProcessor.ProcessMessageAsync(message, replyTo);
                        
                        // Send a response if a reply-to queue was provided
                        if (!string.IsNullOrEmpty(replyTo))
                        {
                            var responseJson = JsonSerializer.Serialize(result);
                            await _rabbitMqService.PublishResponseAsync(responseJson, replyTo);
                            _logger.LogInformation("Response sent to {ReplyTo} queue", replyTo);
                        }
                        else
                        {
                            _logger.LogInformation("No reply-to queue specified. No response will be sent.");
                        }
                        
                        // Acknowledge the message as processed
                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        _logger.LogInformation("Message acknowledged");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message");
                        
                        try
                        {
                            // Negative acknowledge the message to have it requeued
                            await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                            _logger.LogWarning("Message rejected and requeued");
                        }
                        catch (Exception nackEx)
                        {
                            _logger.LogError(nackEx, "Error rejecting message");
                        }
                    }
                };

                // Start consuming from the queue
                string consumerTag = await _channel.BasicConsumeAsync(
                    queue: _settings.ProcessingQueueName,
                    autoAck: false, // Manual acknowledgment
                    consumer: consumer);

                _logger.LogInformation("Started consuming from queue {Queue} with consumer tag {ConsumerTag}", 
                    _settings.ProcessingQueueName, consumerTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RabbitMQ connection");
                
                // Retry after a delay
                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task CleanupRabbitMQAsync()
        {
            try
            {
                if (_channel != null && _channel.IsOpen)
                {
                    await _channel.CloseAsync();
                }
                
                if (_connection != null && _connection.IsOpen)
                {
                    await _connection.CloseAsync();
                }
                
                _logger.LogInformation("RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up RabbitMQ resources");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Message Consumer Service is stopping");
            
            await CleanupRabbitMQAsync();
            
            await base.StopAsync(cancellationToken);
        }
    }
}