using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class ItemLocale
    {
        public ItemLocale()
        {
            PrimaryInformation = new PrimaryInformation();
        }

        [XmlAttribute(AttributeName = "Country")]
        public string Country;

        [XmlAttribute(AttributeName = "Language")]
        public string Language;

        [XmlElement("PrimaryInformation")]
        public PrimaryInformation PrimaryInformation;

    }
}
