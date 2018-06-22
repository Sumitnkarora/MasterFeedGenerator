using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.PosData.Models
{
    /// <summary>
    /// Item Class reprsents each line item in a POS transaction.
    /// </summary>
    public class Item
    {
        [XmlAttribute]
        public string ItemId { get; set; }

        [XmlAttribute]
        public int Quantity { get; set; }

        [XmlAttribute]
        public string Reference_1 { get; set; }

        [XmlAttribute]
        public string ShipNode { get; set; }

        [XmlAttribute]
        public string TransactionDate { get; set; }
    }
}
