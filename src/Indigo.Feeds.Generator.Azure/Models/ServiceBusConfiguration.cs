namespace Indigo.Feeds.Generator.Azure.Models
{
    public class ServiceBusConfiguration
    {
        public ServiceBusConfiguration(string connectionString, string destinationName, string contentType)
        {
            ConnectionString = connectionString;
            DestinationName = destinationName;
            ContentType = contentType;
        }

        public string ConnectionString;

        public string DestinationName;

        public string ContentType;
    }
}
