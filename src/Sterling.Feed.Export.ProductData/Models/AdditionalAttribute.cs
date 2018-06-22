using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class AdditionalAttribute
    {
        [XmlAttribute(AttributeName = "AttributeDomainID")]
        public string AttributeDomainID;

        [XmlAttribute(AttributeName = "AttributeGroupID")]
        public string AttributeGroupID;

        [XmlAttribute(AttributeName = "Operation")]
        public string Operation;

        [XmlAttribute(AttributeName = "Name")]
        public string Name;

        [XmlAttribute(AttributeName = "Value")]
        public string Value;
    }
}
