using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Sterling.Feed.Export.CustomerData.Models
{
    public class CustomerContact : AddressInfo
    {
        public CustomerContact () : base() { }

        public CustomerContact(AddressInfo addressInfo, string customerID) : base()
        {
            CustomerContactID = customerID;
            AddressID = addressInfo.AddressID;
            AddressIdOrNickname = addressInfo.AddressIdOrNickname;
            AddressNickname = addressInfo.AddressNickname;
            AddressLine1 = addressInfo.AddressLine1;
            AddressLine2 = addressInfo.AddressLine2;
            AddressType = addressInfo.AddressType;
            City = addressInfo.City;
            Country = addressInfo.Country;
            DayPhone = addressInfo.DayPhone;
            EmailAddress = addressInfo.EmailAddress;
            FirstName = addressInfo.FirstName;
            LastName = addressInfo.LastName;
            MiddleInitial = addressInfo.MiddleInitial;
            SMSPhoneNum = addressInfo.SMSPhoneNum;
            EveningPhone = addressInfo.EveningPhone;
            PreferredShipAddress = addressInfo.PreferredShipAddress;
            ProvinceCode = addressInfo.ProvinceCode;
            PostalZip = addressInfo.PostalZip;
            Title = addressInfo.Title;
        }

        [XmlAttribute]
        public string CustomerContactID { get; set; }

        public CustomerAdditionalAddressList CustomerAdditionalAddressList { get; set; } = new CustomerAdditionalAddressList();
    }
}
