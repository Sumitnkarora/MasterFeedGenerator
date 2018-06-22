using System;
using System.Collections.Generic;
using System.IO;
using Castle.Core.Logging;
using System.Text;
using Indigo.Feeds.Utils;

namespace NewGoogleCategoriesForIndigoCategoriesImporter.Input
{
    public class InputDataProvider : IInputDataProvider
    {
        private readonly Func<TextReader> createReader;
        private ILogger Log { get; set; }

        // Mock constructor
        public InputDataProvider(Func<TextReader> createReader, ILogger logger)
        {
            this.createReader = createReader;
            this.Log = logger;
        }
        
        // Production constructor
        public InputDataProvider(string csvPath, ILogger logger)
        {
            this.createReader = () => new StreamReader(csvPath);
            this.Log = logger;
        }

        public IList<InputRecord> GetInputSet()
        {
            this.Log.Debug("Entering GetInputSet method.");

            var result = new List<InputRecord>();

            int lineCount = 1;
            using (var reader = this.createReader())
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    var record = this.ParseLine(line, lineCount);
                    result.Add(record);

                    lineCount++;
                }
            }

            this.Log.Debug("Exiting GetInputSet method.");

            return result;
        }

        private static readonly string Separator = ParameterUtils.GetParameter<string>("Separator");

        private InputRecord ParseLine(string line, int lineCount)
        {
            var parts = line.Split(new[] {Separator}, System.StringSplitOptions.None);

            if (parts.Length != 4)
            {
                HandleBadLine("CSV parameter count wrong.", line, lineCount);
            }

            int indigoCategoryId = 0;
            try
            {
                indigoCategoryId = int.Parse(parts[0].Trim());
            }
            catch (FormatException)
            {
                HandleBadLine("Indigo category ID in a bad format.", line, lineCount);
            }
            catch (OverflowException)
            {
                HandleBadLine("Indigo category ID integer overflow.", line, lineCount);
            }

            var indigoBreadCrumb = parts[1].Trim();
            var currentGoogleBreadCrumb = parts[2].Trim();
            var newGoogleBreadCrumb = parts[3].Trim();

            InputRecord result = new InputRecord
            {
                IndigoCategoryId = indigoCategoryId,
                IndigoBreadcrumb = indigoBreadCrumb,
                CurrentGoogleBreadcrumb = currentGoogleBreadCrumb,
                NewGoogleBreadcrumb = newGoogleBreadCrumb,
            };

            return result;
        }

        private const string BadLineMessage = "Line Number {0}. Line: {1}\n";

        private static void HandleBadLine(string message, string line, int lineCount)
        {
            message += " " + string.Format(BadLineMessage, lineCount, line);
            throw new ArgumentException(message);
        }
    }
}
