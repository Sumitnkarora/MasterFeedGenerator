using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.CustomerData.Models
{
    public class CustomerAdditionalAddressList
    {
        private const string RESET = "Y";

        [XmlAttribute]
        public string Reset { get; set; } = RESET;

        [XmlElement("CustomerAdditionalAddress")]
        public List<CustomerAdditionalAddress> Addresses { get; set; }
    }
}
