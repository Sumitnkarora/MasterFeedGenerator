using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.ProductData.Models
{
    public class SafetyFactorDefinitions
    {
        public SafetyFactorDefinitions()
        {
            SafetyFactorDefinition = new SafetyFactorDefinition();
        }

        [XmlAttribute(AttributeName = "Reset")]
        public string Reset;

        [XmlElement("SafetyFactorDefinition")]
        public SafetyFactorDefinition SafetyFactorDefinition;

    }
}
