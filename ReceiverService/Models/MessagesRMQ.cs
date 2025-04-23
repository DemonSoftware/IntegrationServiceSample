using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ReceiverService.Models
{
    public class ProducedMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
    
        public string RequestId { get; set; } = string.Empty;
        public string MessageContent { get; set; } = string.Empty;
        public DateTime ProducedAt { get; set; } = DateTime.UtcNow;
        public bool SentToRabbitMQ { get; set; } = false;
        public string? RabbitMQStatus { get; set; }
        public DateTime? SentToRabbitMQAt { get; set; }
        public string? ErrorDetails { get; set; }
    }

    
    public class ReceivedMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
    
        public string MessageId { get; set; }
        public string RequestId { get; set; }
        public string SourceQueue { get; set; }
        public string MessageContent { get; set; }
        public string Status { get; set; } // RECEIVED, PROCESSING, PROCESSED, FAILED
        public DateTime ReceivedAt { get; set; }
        public DateTime? ProcessingStartedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public string ErrorMessage { get; set; }
        public string ResponseContent { get; set; }
    }
}

