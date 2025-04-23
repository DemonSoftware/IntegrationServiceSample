using ReceiverService.Models;

namespace ReceiverService.Repositories
{
    public interface IMessageRepository
    {
        Task<string> SaveProducedMessageAsync(ProducedMessage message);
        Task UpdateMessageSentStatusAsync(string id, bool success, string status, string? errorDetails = null);
    }
}

