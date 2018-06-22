namespace Indigo.Feeds.Generator.Core.Models
{
    public class FtpConfiguration
    {
        public FtpConfiguration(string host, string userName, string userPassword, string dropFolder, uint bufferSize)
        {
            Host = host;
            UserName = userName;
            UserPassword = userPassword;
            DropFolderPath = dropFolder;
            BufferSize = bufferSize;
        }

        public uint BufferSize;
        public string DropFolderPath;
        public string Host;
        public string UserName;
        public string UserPassword;
    }
}
