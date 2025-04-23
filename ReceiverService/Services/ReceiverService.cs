using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReceiverService.Models;
using ReceiverService.Repositories;
using ReceiverService.Messaging;
using System.Text.Json;

namespace ReceiverService.Services
{
    public class ReceiverService(IMongoDbRepository repository,
            IRabbitMqService rabbitMqService,
            ILogger<ReceiverService> logger, IMessageRepository messageRepository)
        : IReceiverService
    {
        private readonly IMongoDbRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        private readonly IRabbitMqService _rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
        private readonly ILogger<ReceiverService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IMessageRepository _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));

        public async Task<ProcessingResult> ProcessJsonRequestAsync(JsonRequest request)
        {
            string messageStorageId = string.Empty;
            try
            {
                _logger.LogInformation("Processing new JSON request with ID: {RequestId}", request.RequestId);
                
                // Save request to MongoDB (including the outbox status)
                var id = await _repository.SaveJsonRequestAsync(request);
                _logger.LogInformation("JSON request saved to MongoDB with ID: {Id}", id);
                
                try
                {
                    // Create a message with just the essential data for processing
                    var message = new
                    {
                        request.RequestId,
                        request.Content,
                        request.RequestDate,
                        request.Source
                    };
                    
                    // Serialize the message
                    var messageJson = JsonSerializer.Serialize(message);
                    
                    // Save produced message to MessagesRMQ database before sending to RabbitMQ
                    var producedMessage = new ProducedMessage
                    {
                        RequestId = request.RequestId,
                        MessageContent = messageJson,
                        SentToRabbitMQ = true,
                        SentToRabbitMQAt = DateTime.Now,
                        RabbitMQStatus = "Pending"
                    };
                    
                    messageStorageId = await _messageRepository.SaveProducedMessageAsync(producedMessage);
                    _logger.LogInformation("Produced message saved to MongoDB for request ID: {RequestId}", request.RequestId);
                    
                    // Publish to RabbitMQ and wait for response from the consumer
                    var responseJson = await _rabbitMqService.PublishAndWaitResponseAsync(messageJson, TimeSpan.FromSeconds(15));
                    
                    // Deserialize the response
                    var response = JsonSerializer.Deserialize<ProcessingResult>(responseJson);
                    
                    if (response?.Success == true)
                    {
                        // Mark as processed in outbox
                        await _repository.SetOutboxStatusProcessedAsync(id);
                        _logger.LogInformation("Message successfully processed by consumer for request ID: {RequestId}", request.RequestId);
                    }
                    else
                    {
                        // Processing failed on the consumer side
                        await _repository.SetOutboxStatusFailedAsync(id, response?.ErrorDetails ?? "Unknown error from consumer");
                        _logger.LogWarning("Message processing failed on consumer side for request ID: {RequestId}. Error: {Error}", 
                            request.RequestId, response?.ErrorDetails ?? "Unknown error");
                    }
                    
                    return response ?? new ProcessingResult { 
                        RequestId = request.RequestId,
                        Success = false,
                        Message = "Failed to deserialize processing result",
                        ErrorDetails = "Invalid response from consumer"
                    };
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout waiting for response from consumer for request ID: {RequestId}", request.RequestId);
                    
                    // Message went to RabbitMQ but we don't know if it was processed
                    // We'll keep it in outbox for retrying later
                    await _repository.IncrementOutboxRetryCountAsync(id);
                    
                    return new ProcessingResult
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Message = "Timeout waiting for processing confirmation",
                        ErrorDetails = "Consumer did not respond within the timeout period"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish message to RabbitMQ for request ID: {RequestId}", request.RequestId);
                    
                    // Publication failed - message will remain in outbox for retry
                    await _repository.SetOutboxStatusFailedAsync(id, ex.Message);
                    await _repository.IncrementOutboxRetryCountAsync(id);
                    
                    return new ProcessingResult
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Message = "Failed to send message for processing",
                        ErrorDetails = ex.Message
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing JSON request");
                throw;
            }
        }
    }
}