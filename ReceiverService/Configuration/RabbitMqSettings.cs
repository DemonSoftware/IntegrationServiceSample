namespace ReceiverService.Configuration
{
    public class RabbitMqSettings
    {
        public string ConnectionString { get; set; }
        public string ProcessingQueueName { get; set; }
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
        public int RetryCount { get; set; }
        public int RetryIntervalSeconds { get; set; }
    }
}