using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class SafetyFactorDefinition
    {
        [XmlAttribute(AttributeName = "DeliveryMethod")]
        public string DeliveryMethod;

        [XmlAttribute(AttributeName = "OnhandSafetyFactorQuantity")]
        public decimal OnhandSafetyFactorQuantity;
    }
}
