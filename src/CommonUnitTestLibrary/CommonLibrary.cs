using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Moq;

namespace CommonUnitTestLibrary
{
    public class CommonLibrary
    {
        public static string NewGuid { get { return Guid.NewGuid().ToString("N"); }}

        public static Mock<ILogger> SetupLogger()
        {
            var result = new Mock<ILogger>();

            result.Setup(m => m.Error(It.IsAny<string>()))
                .Callback<string>(m => WriteToConsole(m, "Error"));

            result.Setup(m => m.ErrorFormat(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((m, p) => WriteToConsole(m, "Error", p));

            result.Setup(m => m.Info(It.IsAny<string>()))
                .Callback<string>(m => WriteToConsole(m, "Info"));

            result.Setup(m => m.InfoFormat(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((m, p) => WriteToConsole(m, "Info", p));

            result.Setup(m => m.Debug(It.IsAny<string>()))
                .Callback<string>(m => WriteToConsole(m, "Debug"));

            result.Setup(m => m.DebugFormat(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((m, p) => WriteToConsole(m, "Debug", p));

            result.Setup(m => m.Warn(It.IsAny<string>()))
                .Callback<string>(m => WriteToConsole(m, "Warn"));

            result.Setup(m => m.WarnFormat(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((m, p) => WriteToConsole(m, "Warn", p));

            return result;
        }

        private static void WriteToConsole(string message, string level)
        {
            Console.WriteLine(level + ": " + message + "\n");
        }

        private static void WriteToConsole(string message, string level, params object[] parameters)
        {
            Console.WriteLine(level + ": " + message + "\n", parameters);
        }
    }
}
