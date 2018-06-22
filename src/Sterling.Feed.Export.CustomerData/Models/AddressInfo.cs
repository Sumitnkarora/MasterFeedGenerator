using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.CustomerData.Models
{
    public class AddressInfo
    {
        [XmlAttribute("AddressID")]
        public string AddressIdOrNickname { get; set; }

        [XmlIgnore]
        public string AddressID { get; set; }

        [XmlIgnore]
        public string AddressNickname { get; set; }

        [XmlAttribute]
        public string AddressLine1 { get; set; }

        [XmlAttribute]
        public string AddressLine2 { get; set; }

        [XmlIgnore]
        public string AddressType { get; set; }

        [XmlAttribute]
        public string City { get; set; }

        [XmlAttribute("Country")]
        public string Country { get; set; }

        [XmlAttribute]
        public string DayPhone { get; set; }

        [XmlAttribute("EmailID")]
        public string EmailAddress { get; set; }

        [XmlAttribute]
        public string FirstName { get; set; }

        [XmlAttribute]
        public string LastName { get; set; }

        [XmlAttribute("MiddleName")]
        public string MiddleInitial { get; set; }

        [XmlAttribute("MobilePhone")]
        public string SMSPhoneNum { get; set; }

        [XmlAttribute("OtherPhone")]
        public string EveningPhone { get; set; }

        [XmlAttribute]
        public string PreferredShipAddress { get; set; } = "N";
                
        [XmlAttribute("State")]
        public string ProvinceCode { get; set; }

        [XmlAttribute("ZipCode")]
        public string PostalZip { get; set; }

        [XmlAttribute]
        public string Title { get; set; }
    }
}
