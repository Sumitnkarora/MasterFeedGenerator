using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace FeedGenerators.Core.Utils
{
    public static class ParameterUtils
    {
        private const string BadConfigLogTemplate = "{0} configuration setting is empty or missing!";

        public static T GetParameter<T>(string settingKey)
        {
            var settingValue = ConfigurationManager.AppSettings[settingKey];

            if (string.IsNullOrEmpty(settingValue))
            {
                // Problem with the parameter... Log an error and throw an error    
            }

            var typeCode = Type.GetTypeCode(typeof(T));

            switch (typeCode)
            {
                case TypeCode.String:
                    return CastStringToType<T>(settingValue);
                case TypeCode.Boolean:
                    if (settingValue.Equals("true", StringComparison.InvariantCultureIgnoreCase) ||
                        settingValue.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return CastStringToType<T>(settingValue);
                    }
                    break;
                case TypeCode.Int32:
                    int tempInt;
                    if (Int32.TryParse(settingValue, out tempInt))
                    {
                        return CastStringToType<T>(settingValue);
                    }
                    break;
                case TypeCode.Int64:
                    long tempLong;
                    if (Int64.TryParse(settingValue, out tempLong))
                    {
                        return CastStringToType<T>(settingValue);
                    }
                    break;
            }

            throw new ConfigurationErrorsException(string.Format(BadConfigLogTemplate, settingKey));
        }

        private static T CastStringToType<T>(string settingValue)
        {
            return (T)Convert.ChangeType(settingValue, typeof(T));
        }
    }
}
