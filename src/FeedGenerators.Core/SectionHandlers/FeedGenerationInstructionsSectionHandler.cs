using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace FeedGenerators.Core.SectionHandlers
{
    class FeedGenerationInstructionsSectionHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            var feedGenerationInstructionsDict = new Dictionary<string, Dictionary<string, string>>();
            if (section.ChildNodes.Count == 0)
                return feedGenerationInstructionsDict;

            foreach (XmlNode childNode in section.ChildNodes)
            {
                if (childNode.Attributes == null || childNode.Attributes["key"] == null) continue;
                var key = childNode.Attributes["key"].Value;
                var dictAttr = new Dictionary<string, string>(childNode.Attributes.Count);
                foreach (XmlAttribute attribute in childNode.Attributes)
                {
                    dictAttr.Add(attribute.Name, attribute.Value);
                }
                feedGenerationInstructionsDict.Add(key, dictAttr);
            }
            return feedGenerationInstructionsDict;
        }
    }
}
