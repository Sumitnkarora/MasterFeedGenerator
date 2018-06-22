using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Xml;

namespace FeedGenerators.Core.SectionHandlers
{
    class ItemFormatSectionHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            var itemFormarDict = new StringDictionary();
            if (section.ChildNodes.Count == 0)
            {
                return itemFormarDict;
            }

            foreach (XmlNode childNode in section.ChildNodes)
            {
                if (childNode.Attributes != null && childNode.Attributes["key"] != null && childNode.Attributes["value"] != null)
                {
                    itemFormarDict.Add(childNode.Attributes["key"].Value, childNode.Attributes["value"].Value);
                }
            }
            return itemFormarDict;
        }
    }
}
