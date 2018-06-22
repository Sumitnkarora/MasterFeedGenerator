using System;

namespace Indigo.Feeds.Generator.Core.Enums
{
    [Flags]
    public enum OutputFormat
    {
        Delete = 1, 
        Insert = 2,
        Update = 4
    }
}
