using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.Text;
using Castle.Core.Logging;
using FeedGenerators.Core.Services.Abstract;
using FeedGenerators.Core.Enums;

namespace RRFeedGenerator.Execution.FileFeedWriter.Helpers
{
    class AttributeOutput
    {
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        private readonly FeedWriterContext _context;      
        private readonly string _pid;
        private readonly Utility _utility;
        private readonly char _delimiter;

        private readonly bool _excludeIfEmpty;

        public AttributeOutput(string pidKey, IDataReader reader, char delimiter,
            FeedWriterContext context, Utility utility, bool excludeIfEmpty = true)
        {
            _utility = utility;
            _pid = _utility.GetAttributeValue(pidKey).ToString();
            _delimiter = delimiter;
            _context = context;
            _excludeIfEmpty = excludeIfEmpty;
        }

        private Func<IDictionary<Language, string>, string> LanguageWriter
        {
            get { return _context.WriteLanguage; }
        }

        private ILogger Log
        {
            get { return _context.Log; }
        }

        private Language Language
        {
            get { return _context.Language; }
        }

        #region Public Methods

        public AttributeOutput Add(string attributeName, string key)
        {
            return Add<object>(attributeName, key, null);
        }

        public AttributeOutput Add<T>(string attributeName, string key, Func<T, string> formatter)
        {
            var valueObject = _utility.GetAttributeValue(key);
            var value = PostProcessResult((T)valueObject.DbNullToNull(), formatter);

            if (_excludeIfEmpty && string.IsNullOrWhiteSpace(value))
                return this;

            AppendAttributeName(attributeName);
            AppendValue(value);

            return this;
        }

        public AttributeOutput Add(string attributeName, string english, string french)
        {
            return Add<object>(attributeName, english, french, null);
        }

        public AttributeOutput Add<T>(string attributeName, string english, string french,
            Func<T, string> formatter)
        {
            Log.Debug("Enter Add(string attributeName, string english, string french)");

            var localizedKey = _utility.GetLocalizedKey(LanguageWriter, english, french);

            if (string.IsNullOrEmpty(localizedKey))
            {
                Log.Debug("localizedKey returned null or empty. Note: Returning.");
                return this;
            }

            var resultObject = _utility.GetLocalizedResult(Language, localizedKey, english);
            var result = PostProcessResult((T)resultObject.DbNullToNull(), formatter);

            if (_excludeIfEmpty && string.IsNullOrWhiteSpace(result))
                return this;

            AppendAttributeName(attributeName);
            AppendValue(result);

            return this;
        }

        public AttributeOutput AddLiteral(string attributeName, string literal)
        {
            if (_excludeIfEmpty && string.IsNullOrWhiteSpace(literal))
                    return this;
            
            AppendAttributeName(attributeName);
            AppendValue(literal);

            return this;
        }

        public override string ToString()
        {
            return _stringBuilder.ToString();
        }

        #endregion Public Methods

        #region Private Methods

        private void AppendValue(string value)
        {
            _stringBuilder.Append((value ?? string.Empty) + "\n");
        }

        private void AppendAttributeName(string attributeName)
        {
            _stringBuilder.Append(_pid + _delimiter.ToString(CultureInfo.InvariantCulture));

            attributeName = attributeName.ToUpper();
            _stringBuilder.Append(attributeName + _delimiter.ToString(CultureInfo.InvariantCulture));
        }

        private static string PostProcessResult<T>(T value, Func<T, string> formatter)
        {
            string result = null;

            if (value == null && formatter == null)
                result = string.Empty;
            
            else if (formatter != null)
                result = formatter(value);

            else if (value != null)
                result = value.ToString();

            return result;
        }

        #endregion Private Methods
    }
}
