using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.CustomerData.Models
{
    public class CustomerCurrency
    {
        private const string CURR = "CAD";
        private const string IS_DEFAULT = "Y";

        [XmlAttribute]
        public string Currency { get; set; } = CURR;

        [XmlAttribute]
        public string IsDefaultCurrency { get; set; } = IS_DEFAULT;
    }
}
