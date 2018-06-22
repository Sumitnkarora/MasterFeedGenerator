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
    class OutputLine
    {
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private readonly FeedWriterContext _context;
        private readonly Utility _utility;
        private readonly char _delimiter;

        public OutputLine(char delimiter, FeedWriterContext context, Utility utility)
        {
            _context = context;
            _utility = utility;
            _delimiter = delimiter;
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

        public OutputLine Add(string attributeKey, Func<object, string> formatter = null)
        {
            if (string.IsNullOrEmpty(attributeKey))
            {
                _stringBuilder.Append(_delimiter);
                return this;
            }

            var result = _utility.GetAttributeValue(attributeKey);
            return AppendToStringBuilder(result, formatter);
        }

        public OutputLine Add(string english, string french, 
            Func<object, string> formatter = null)
        {
            Log.Debug("Enter Add(string english, string french)");

            var localizedKey = _utility.GetLocalizedKey(LanguageWriter, english, french);

            if (string.IsNullOrEmpty(localizedKey))
            {
                Log.Debug("localizedKey returned null or empty. Note: Returning.");
                _stringBuilder.Append(_delimiter);
                return this;
            }

            var result = _utility.GetLocalizedResult(Language, localizedKey, english);

            Log.Debug("Returning from Add(string english, string french)");

            return AppendToStringBuilder(result, formatter);
        }

        private OutputLine AppendToStringBuilder(object result, Func<object, string> formatter)
        {
            if (formatter != null)
                result = formatter(result);

            _stringBuilder.Append(result + _delimiter.ToString(CultureInfo.InvariantCulture));

            return this;
        }

        public OutputLine AddLiteral(string literal, Func<string, string> formatter = null)
        {
            if (formatter != null)
                literal = formatter(literal);

            _stringBuilder.Append(literal + _delimiter.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public OutputLine AddLiteral(Func<string> method, Func<string, string> formatter = null)
        {
            return AddLiteral(method(), formatter);
        }

        public override string ToString()
        {
            return Utility.LineToString(_stringBuilder);
        }
    }
}
