using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ReceiverService.Configuration;
using ReceiverService.Models;
using System.Linq;

namespace ReceiverService.Repositories
{
    public class RequestsDbRepository : IMongoDbRepository
    {
        private readonly IMongoCollection<JsonRequest> _requests;
        
        public RequestsDbRepository(IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            var database = client.GetDatabase(settings.Value.DatabaseName);
            _requests = database.GetCollection<JsonRequest>(settings.Value.JsonRequestsCollection);
        }

        public async Task<string> SaveJsonRequestAsync(JsonRequest request)
        {
            await _requests.InsertOneAsync(request);
            return request.Id;
        }

        public async Task<JsonRequest> GetJsonRequestByIdAsync(string id)
        {
            return await _requests.Find(r => r.Id == id).FirstOrDefaultAsync();
        }

        public async Task<JsonRequest> GetJsonRequestByRequestIdAsync(string requestId)
        {
            return await _requests.Find(r => r.RequestId == requestId).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<JsonRequest>> GetPendingOutboxMessagesAsync(int limit = 50)
        {
            var currentTime = DateTime.UtcNow;
            
            var filter = Builders<JsonRequest>.Filter.And(
                Builders<JsonRequest>.Filter.Eq(r => r.OutboxStatus.Status, "PENDING"),
                Builders<JsonRequest>.Filter.Lte(r => r.OutboxStatus.NextRetryAt, currentTime),
                Builders<JsonRequest>.Filter.Lt(r => r.OutboxStatus.RetryCount, 10) // Max retry count
            );
            
            return await _requests
                .Find(filter)
                .Sort(Builders<JsonRequest>.Sort.Ascending(r => r.OutboxStatus.NextRetryAt))
                .Limit(limit)
                .ToListAsync();
        }

        public async Task UpdateOutboxStatusAsync(string id, OutboxStatus status)
        {
            var filter = Builders<JsonRequest>.Filter.Eq(r => r.Id, id);
            var update = Builders<JsonRequest>.Update.Set(r => r.OutboxStatus, status);
            
            await _requests.UpdateOneAsync(filter, update);
        }

        public async Task<bool> SetOutboxStatusProcessedAsync(string id)
        {
            var filter = Builders<JsonRequest>.Filter.Eq(r => r.Id, id);
            var update = Builders<JsonRequest>.Update
                .Set(r => r.OutboxStatus.Status, "PROCESSED")
                .Set(r => r.OutboxStatus.ProcessedDate, DateTime.UtcNow);
            
            var result = await _requests.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> SetOutboxStatusFailedAsync(string id, string errorDetails)
        {
            var filter = Builders<JsonRequest>.Filter.Eq(r => r.Id, id);
            var update = Builders<JsonRequest>.Update
                .Set(r => r.OutboxStatus.Status, "FAILED")
                .Set(r => r.OutboxStatus.ErrorDetails, errorDetails)
                .Set(r => r.OutboxStatus.LastRetryDate, DateTime.UtcNow);
            
            var result = await _requests.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> IncrementOutboxRetryCountAsync(string id)
        {
            var filter = Builders<JsonRequest>.Filter.Eq(r => r.Id, id);
            
            // Calculate next retry with exponential backoff
            var request = await GetJsonRequestByIdAsync(id);
            if (request == null) return false;
            
            int retryCount = request.OutboxStatus.RetryCount + 1;
            var backoffSeconds = Math.Pow(2, retryCount) * 5; // 5, 10, 20, 40, 80 seconds...
            backoffSeconds = Math.Min(backoffSeconds, 3600); // Max 1 hour
            
            // Add a small jitter (±20%)
            var random = new Random();
            var jitter = (random.NextDouble() * 0.4) - 0.2; // -20% to +20%
            backoffSeconds = backoffSeconds * (1 + jitter);
            
            var nextRetryAt = DateTime.UtcNow.AddSeconds(backoffSeconds);
            
            var update = Builders<JsonRequest>.Update
                .Inc(r => r.OutboxStatus.RetryCount, 1)
                .Set(r => r.OutboxStatus.LastRetryDate, DateTime.UtcNow)
                .Set(r => r.OutboxStatus.NextRetryAt, nextRetryAt);
            
            var result = await _requests.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
    }
}