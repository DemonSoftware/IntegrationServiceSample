namespace ProcessingService.Configuration
{
    public class RabbitMqSettings
    {
        public string ConnectionString { get; set; }
        public string ProcessingQueueName { get; set; }
        public string ResponseQueueName { get; set; }
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
    }
}