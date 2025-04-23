using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProcessingService.Data;
using ProcessingService.Models;

namespace ProcessingService.Services
{
    public interface IMessageProcessor
    {
        Task<ProcessingResult> ProcessMessageAsync(string messageJson, string replyTo);
    }

    public class MessageProcessor(ISqlRepository repository, ILogger<MessageProcessor> logger)
        : IMessageProcessor
    {
        private readonly ISqlRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        private readonly ILogger<MessageProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<ProcessingResult> ProcessMessageAsync(string messageJson, string replyTo)
        {
            _logger.LogInformation("Processing incoming message with reply-to: {ReplyTo}", replyTo);

            try
            {
                // Parse the incoming message
                var incomingMessage = JsonSerializer.Deserialize<IncomingMessage>(messageJson);
                
                if (incomingMessage == null)
                {
                    _logger.LogError("Failed to deserialize incoming message");
                    return new ProcessingResult
                    {
                        Success = false,
                        Message = "Failed to deserialize message",
                        ErrorDetails = "The message format was invalid"
                    };
                }

                // Parse the JSON content inside the message
                OrderData orderData;
                try
                {
                    orderData = JsonSerializer.Deserialize<OrderData>(incomingMessage.Content);
                    
                    if (orderData == null)
                    {
                        throw new JsonException("Order data is null");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse order data from message content");
                    return new ProcessingResult
                    {
                        RequestId = incomingMessage.RequestId,
                        Success = false,
                        Message = "Invalid order data format",
                        ErrorDetails = ex.Message
                    };
                }

                // Validate the order data
                if (string.IsNullOrEmpty(orderData.OrderNumber))
                {
                    _logger.LogError("Order validation failed for request ID {RequestId}", incomingMessage.RequestId);
                    return new ProcessingResult
                    {
                        RequestId = incomingMessage.RequestId,
                        Success = false,
                        Message = "Order validation failed",
                        ErrorDetails = "Missing required fields: Order Number, Customer Name, or Items"
                    };
                }

                // Process the order
                try
                {
                    // Save to SQL database
                    var orderId = await _repository.SaveOrderAsync(orderData);
                    
                    _logger.LogInformation("Successfully processed order {OrderNumber} for request {RequestId}", 
                        orderData.OrderNumber, incomingMessage.RequestId);
                    
                    // Return success result
                    return new ProcessingResult
                    {
                        RequestId = incomingMessage.RequestId,
                        Success = true,
                        Message = $"Order {orderData.OrderNumber} successfully processed",
                        OrderId = orderId
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving order to database for request {RequestId}", 
                        incomingMessage.RequestId);
                    
                    return new ProcessingResult
                    {
                        RequestId = incomingMessage.RequestId,
                        Success = false,
                        Message = "Failed to save order to database",
                        ErrorDetails = ex.Message
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message");
                return new ProcessingResult
                {
                    Success = false,
                    Message = "Unexpected error occurred while processing message",
                    ErrorDetails = ex.Message
                };
            }
        }
    }
}