using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ReceiverService.Models;
using ReceiverService.Services;
using System.Linq;
using ReceiverService.Messaging;

namespace ReceiverService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReceiverController(IReceiverService receiverService,
            ILogger<ReceiverController> logger, IRabbitMqService _rabbitMqService)
        : ControllerBase
    {
        private readonly IReceiverService _receiverService = receiverService ?? throw new ArgumentNullException(nameof(receiverService));
        private readonly ILogger<ReceiverController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IRabbitMqService _rabbitMqService = _rabbitMqService;

        [HttpPost("Receive")]
        public async Task<IActionResult> ReceiveJson()
        {
            try
            {
                _logger.LogInformation("Received a new JSON request");

                // Read the content of the request
                using var reader = new StreamReader(Request.Body);
                var jsonContent = await reader.ReadToEndAsync();

                // Check if content is empty
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Received empty JSON content");
                    return BadRequest(new { Error = "JSON content cannot be empty" });
                }

                // Create a JsonRequest object
                var jsonRequest = new JsonRequest
                {
                    Content = jsonContent,
                    ContentType = Request.ContentType,
                    ContentLength = Request.ContentLength,
                    Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
                };

                // Process the request
                var result = await _receiverService.ProcessJsonRequestAsync(jsonRequest);

                // Return response based on processing result
                if (result.Success)
                {
                    return Ok(new
                    {
                        RequestId = result.RequestId,
                        Status = "Processed",
                        Message = result.Message ?? "JSON successfully received and processed"
                    });
                }

                return StatusCode(500, new
                {
                    RequestId = result.RequestId,
                    Status = "Failed",
                    Error = result.ErrorDetails ?? "An error occurred during processing",
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing JSON request");
                
                return StatusCode(500, new
                {
                    Error = "An error occurred while processing the request",
                    Message = ex.Message
                });
            }
        }
        
        [HttpPost("test")]
        public async Task<IActionResult> ReceiveJsonTest()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var jsonContent = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    return BadRequest(new { Error = "JSON content cannot be empty" });
                }

                // If routing key is provided, you could pass it to the publish method
                await _rabbitMqService.PublishAsync(jsonContent);

                return Accepted(new
                {
                    Status = "Message sent to RabbitMQ",
                    Timestamp = DateTime.UtcNow,
                    ContentLength = jsonContent.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test JSON request handling");
        
                return StatusCode(500, new
                {
                    Error = "An error occurred while handling the test request",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("status/{requestId}")]
        public async Task<IActionResult> GetStatus(string requestId)
        {
            try
            {
                // Here you would implement logic to check the status of a request
                // by its requestId from MongoDB
                
                // For now, this is a placeholder that will be implemented later
                return Ok(new { RequestId = requestId, Status = "Processing" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status for request {RequestId}", requestId);
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}