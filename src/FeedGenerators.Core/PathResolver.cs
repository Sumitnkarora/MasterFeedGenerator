using System;
using System.IO;
using Indigo.Feeds.Utils;

namespace FeedGenerators.Core
{
    public class PathResolver
    {
        private const string Key_InputFileFolderPath = "InputFileFolderPath";
        private const string Key_InputFileName = "InputFileName";

        private static readonly string InputFileFolderPath = ParameterUtils.GetParameter<string>(Key_InputFileFolderPath);
        private static readonly string InputFileName = ParameterUtils.GetParameter<string>(Key_InputFileName);

        public virtual string GetInputFilePath()
        {
            Assert(Key_InputFileFolderPath, InputFileFolderPath);
            Assert(Key_InputFileName, InputFileName);

            return Path.Combine(InputFileFolderPath, InputFileName);
        }

        public virtual bool HasInputFile()
        {
            return File.Exists(this.GetInputFilePath());
        }

        private static void Assert(string key, string value)
        {
            if (value == null)
            {
                throw new ArgumentException("No config data for key: " + key);
            }
        }
    }
}
