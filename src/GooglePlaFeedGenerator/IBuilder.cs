using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GooglePlaFeedGenerator
{
    public interface IBuilder
    {
        void Build(string[] args);
    }
}
