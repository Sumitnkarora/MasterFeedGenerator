using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class AdditionalAttributeList
    {
        public AdditionalAttributeList()
        {
            AdditionalAttribute = new List<AdditionalAttribute>();
        }

        [XmlAttribute(AttributeName = "Reset")]
        public string Reset;

        [XmlElement("AdditionalAttribute")]
        public List<AdditionalAttribute> AdditionalAttribute;

    }
}
