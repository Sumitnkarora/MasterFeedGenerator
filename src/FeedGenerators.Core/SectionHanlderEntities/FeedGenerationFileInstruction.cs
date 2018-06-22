using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FeedGenerators.Core.SectionHanlderEntities
{
    public class FeedGenerationFileInstruction
    {
        public string Key { get; set; }
        public string Aid { get; set; }
        public List<FeedGenerationFileLineItem> LineItems { get; set; }
    }
}
