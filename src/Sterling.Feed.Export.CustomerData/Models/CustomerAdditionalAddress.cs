using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.CustomerData.Models
{
    public class CustomerAdditionalAddress
    {
        [XmlAttribute("CustomerAdditionalAddressID")]
        public string AddressID { get; set; }

        public AddressInfo PersonInfo { get; set; }
    }
}
