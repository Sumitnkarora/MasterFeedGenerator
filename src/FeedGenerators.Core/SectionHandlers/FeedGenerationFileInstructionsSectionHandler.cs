using FeedGenerators.Core.SectionHanlderEntities;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace FeedGenerators.Core.SectionHandlers
{
    class FeedGenerationFileInstructionsSectionHandler : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            var result = new FeedGenerationFileInstructionsConfigurationSection {FeedGenerationFileInstructions = new List<FeedGenerationFileInstruction>()};
            if (section.ChildNodes.Count == 0)
                return result;

            foreach (XmlNode childNode in section.ChildNodes)
            {
                if (childNode.Attributes == null || childNode.Attributes["key"] == null) continue;

                var fileInstruction = new FeedGenerationFileInstruction { Key = childNode.Attributes["key"].Value, Aid = childNode.Attributes["aid"].Value, LineItems = new List<FeedGenerationFileLineItem>() };
                foreach (XmlNode childNodeChild in childNode.ChildNodes)
                {
                    if (childNodeChild == null || childNodeChild.Attributes == null) 
                        continue;

                    var lineItem = new FeedGenerationFileLineItem
                    {
                        Catalog = childNodeChild.Attributes["catalog"].Value,
                        Catalogattributesection = childNodeChild.Attributes["catalogattributesection"].Value,
                        IsIncluded = bool.Parse(childNodeChild.Attributes["isIncluded"].Value),
                        RangeDatas = childNodeChild.Attributes["ranges"].Value,
                        StoredProcedureName = childNodeChild.Attributes["storedProcedureName"].Value
                    };

                    fileInstruction.LineItems.Add(lineItem);
                }
                
                result.FeedGenerationFileInstructions.Add(fileInstruction);
            }
            return result;
        }
    }
}
