using System.Collections.Generic;
using System.Xml.Serialization;

namespace Indigo.Feeds.Generator.Core.Models
{
    /// <summary>
    /// Export data.
    /// </summary>
    public class ExportData : BaseExportData
    {
        [XmlNamespaceDeclarations]
        public override XmlSerializerNamespaces XmlNamespaces
        {
            get
            {
                var xsn = new XmlSerializerNamespaces();
                xsn.Add(string.Empty, string.Empty);
                return xsn;
            }
        }
    }
}
