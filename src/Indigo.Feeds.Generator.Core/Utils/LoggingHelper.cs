using Castle.Core.Logging;
using StackExchange.Exceptional;
using System;
using System.Collections.Generic;

namespace Indigo.Feeds.Generator.Core.Utils
{
    /// <summary>
    /// Helper for logging to multiple log stores.
    /// </summary>
    public static class LoggingHelper
    {
        /// <summary>
        /// Logs error to multiple log stores.
        /// </summary>
        /// <param name="error">Exception to log.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="logger">Optional castle logger, eg. log4net.</param>
        /// <param name="customData">Custom data. Currently only for Exceptional and NewRelic logger.</param>
        public static void Error(Exception error, string message = null, ILogger logger = null, Dictionary<string, string> customData = null)
        {
            LogToCastle(error, message, logger);
            LogToExceptional(error, message, customData);
            LogToNewRelic(error, message, customData);
        }

        /// <summary>
        /// Logs error to multiple log stores.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="logger">Optional castle logger, eg. log4net.</param>
        /// <param name="customData">Custom data. Currently only for Exceptional and NewRelic logger.</param>
        public static void Error(string message, ILogger logger = null, Dictionary<string, string> customData = null)
        {
            LogToCastle(null, message, logger);
            LogToNewRelic(null, message, customData);
        }

        public static void ErrorToNewRelic(string message, Dictionary<string, string> customData = null)
        {
            LogToNewRelic(null, message, customData);
        }

        #region NewRelic

        private static void LogToNewRelic(Exception error, string message = null, Dictionary<string, string> customData = null)
        {
            if (error == null || message == null)
            {
                return;
            }

            if (customData == null)
            {
                customData = new Dictionary<string, string>();
            }

            if (message != null && error != null)
            {
                customData["Message"] = message;
            }

            if (error != null)
            {
                NewRelic.Api.Agent.NewRelic.NoticeError(error, customData);
            }
            else
            {
                NewRelic.Api.Agent.NewRelic.NoticeError(message, customData);
            }
        }

        #endregion NewRelic

        #region Exceptional

        private static void LogToExceptional(Exception error, string message = null, Dictionary<string, string> customData = null)
        {
            if (error == null)
            {
                return;
            }

            if (customData == null)
            {
                customData = new Dictionary<string, string>();
            }

            if (message != null)
            {
                customData["Message"] = message;
            }

            ErrorStore.LogExceptionWithoutContext(error, customData: customData);
        }

        #endregion

        #region log4net

        private static void LogToCastle(Exception error, string message, ILogger logger)
        {
            if (logger == null)
            {
                return;
            }

            if (error != null)
            {
                logger.Error(message ?? error.Message, error);
            }
            else
            {
                logger.Error(message);
            }
        }

        #endregion
    }
}
