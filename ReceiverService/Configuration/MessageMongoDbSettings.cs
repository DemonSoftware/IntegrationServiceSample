namespace ReceiverService.Configuration
{
    public class MessageMongoDbSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string ProducedMessagesCollection { get; set; } = string.Empty;
    }
}

