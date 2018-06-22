using Indigo.Feeds.Generator.Core.Models;
using System;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.CustomerData.Models
{

    public class Customer : ExportData
    {
        private const string CUSTOMER_TYPE = "02";
        private const string OPERATION = "Manage";
        private const string ORG_CODE = "Indigo_CA";
        private const int STATUS = 10;

        [XmlAttribute]
        public string CustomerID { get; set; }
        [XmlAttribute("CustomerRewardsNo")]
        public string LoyaltyNumber { get; set; }
        [XmlAttribute]
        public string CustomerType { get; set; } = CUSTOMER_TYPE;
        [XmlAttribute("ExternalCustomerID")]
        public string MembershipID { get; set; }
        [XmlAttribute]
        public string Operation { get; set; } = OPERATION;
        [XmlAttribute]
        public string OrganizationCode { get; set; } = ORG_CODE;
        [XmlAttribute]
        public int Status { get; set; } = STATUS;
        [XmlAttribute("RegisteredDate")]
        public string CreatedOn { get; set; }
        public Consumer Consumer { get; set; }
        public CustomerContactList CustomerContactList { get; set; } = new CustomerContactList();
        public CustomerCurrencyList CustomerCurrencyList { get; set; } = new CustomerCurrencyList();
        [XmlIgnore]
        public AddressInfo AddressInfo { get; set; }
    }
}
