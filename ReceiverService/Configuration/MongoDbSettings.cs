namespace ReceiverService.Configuration
{
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
        public string JsonRequestsCollection { get; set; }
    }
}