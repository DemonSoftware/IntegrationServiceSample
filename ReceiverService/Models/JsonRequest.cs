using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ReceiverService.Models
{
    public class JsonRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        
        public string Content { get; set; }
        
        public string? ContentType { get; set; }
        
        public long? ContentLength { get; set; }
        
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        
        public string Source { get; set; } = "external-api";
        
        [BsonIgnoreIfNull]
        public OutboxStatus? OutboxStatus { get; set; } = new OutboxStatus();
    }

    public class OutboxStatus
    {
        public string Status { get; set; } = "PENDING";
        
        public int RetryCount { get; set; } = 0;
        
        public DateTime? LastRetryDate { get; set; }
        
        public DateTime? ProcessedDate { get; set; }
        
        [BsonIgnoreIfNull]
        public string ErrorDetails { get; set; }
        
        public DateTime NextRetryAt { get; set; } = DateTime.UtcNow;
    }
}