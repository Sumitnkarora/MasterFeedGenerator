using System.Xml.Serialization;

namespace Indigo.Feeds.Generator.Core.Models
{
    public abstract class BaseExportData
    {
        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        [XmlIgnore]
        public string SourceId { get; set; }

        public abstract XmlSerializerNamespaces XmlNamespaces { get; }
    }
}
