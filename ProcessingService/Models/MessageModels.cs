using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProcessingService.Models
{
    // Incoming message structure from ReceiverService
    public class IncomingMessage
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; }
        
        [JsonPropertyName("content")]
        public string Content { get; set; }
        
        [JsonPropertyName("requestDate")]
        public DateTime RequestDate { get; set; }
        
        [JsonPropertyName("source")]
        public string Source { get; set; }
    }
    
    // JSON structure contained within the IncomingMessage.Content
    public class OrderData
    {
        public string OrderNumber { get; set; }
    }
    
    public class OrderItem
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
    
    // Response message to be sent back to ReceiverService
    public class ProcessingResult
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; }
        
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        [JsonPropertyName("errorDetails")]
        public string ErrorDetails { get; set; }
        
        [JsonPropertyName("processedAt")]
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("orderId")]
        public int? OrderId { get; set; }
    }
}