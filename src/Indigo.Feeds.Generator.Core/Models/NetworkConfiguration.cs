namespace Indigo.Feeds.Generator.Core.Models
{
    public class NetworkConfiguration
    {
        public NetworkConfiguration(string destination, bool allowOverwite)
        {
            Destination = destination;
            AllowOverwrite = allowOverwite;
        }

        public string Destination;

        public bool AllowOverwrite;
    }
}
