using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.CustomerData.Models
{
    public class CustomerCurrencyList
    {
        private const string RESET = "Y";

        [XmlAttribute]
        public string Reset { get; set; } = RESET;

        public CustomerCurrency CustomerCurrency { get; set; } = new CustomerCurrency();
    }
}
