using ReceiverService.Models;

namespace ReceiverService.Repositories
{
    public interface IMongoDbRepository
    {
        Task<string> SaveJsonRequestAsync(JsonRequest request);
        Task<JsonRequest> GetJsonRequestByIdAsync(string id);
        Task<JsonRequest> GetJsonRequestByRequestIdAsync(string requestId);
        Task<IEnumerable<JsonRequest>> GetPendingOutboxMessagesAsync(int limit = 50);
        Task UpdateOutboxStatusAsync(string id, OutboxStatus status);
        Task<bool> SetOutboxStatusProcessedAsync(string id);
        Task<bool> SetOutboxStatusFailedAsync(string id, string errorDetails);
        Task<bool> IncrementOutboxRetryCountAsync(string id);
    }
}

    