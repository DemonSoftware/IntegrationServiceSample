using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ReceiverService.Configuration;
using ReceiverService.Models;

namespace ReceiverService.Repositories
{
    public class MessageRepository : IMessageRepository
{
    private readonly IMongoCollection<ProducedMessage> _producedMessages;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(
        IOptions<MessageMongoDbSettings> messageDbSettings,
        ILogger<MessageRepository> logger)
    {
        _logger = logger;
        
        var mongoClient = new MongoClient(messageDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(messageDbSettings.Value.DatabaseName);
        
        _producedMessages = mongoDatabase.GetCollection<ProducedMessage>(
            messageDbSettings.Value.ProducedMessagesCollection);
    }

    public async Task<string> SaveProducedMessageAsync(ProducedMessage message)
    {
        try
        {
            message.ProducedAt = DateTime.UtcNow;
            await _producedMessages.InsertOneAsync(message);
            _logger.LogInformation("Produced message saved with ID: {Id}", message.Id);
            return message.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving produced message");
            throw;
        }
    }

    public async Task UpdateMessageSentStatusAsync(string id, bool success, string status, string? errorDetails = null)
    {
        try
        {
            var filter = Builders<ProducedMessage>.Filter.Eq(m => m.Id, id);
            var update = Builders<ProducedMessage>.Update
                .Set(m => m.SentToRabbitMQ, success)
                .Set(m => m.RabbitMQStatus, status)
                .Set(m => m.SentToRabbitMQAt, DateTime.UtcNow);
            
            if (!string.IsNullOrEmpty(errorDetails))
            {
                update = update.Set(m => m.ErrorDetails, errorDetails);
            }

            await _producedMessages.UpdateOneAsync(filter, update);
            _logger.LogInformation("Updated produced message status for ID: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating produced message status");
            throw;
        }
    }
}
}

