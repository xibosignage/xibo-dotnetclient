using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using XiboClient.Properties;

namespace XiboClient.Log
{
    class ClientInfoTraceListener : TraceListener
    {
        private ClientInfo _clientInfo;

        public ClientInfoTraceListener(ClientInfo clientInfo)
        {
            _clientInfo = clientInfo;
        }

        /// <summary>
        /// Get the LogType from a string
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        private static LogType GetLogTypeFromString(string category)
        {
            LogType logType;

            if (category == LogType.Audit.ToString())
                logType = LogType.Audit;
            else if (category == LogType.Error.ToString())
                logType = LogType.Error;
            else if (category == LogType.Info.ToString())
                logType = LogType.Info;
            else
                logType = LogType.Audit;

            return logType;
        }

        #region overrides
        public override void Write(string message)
        {
            if (Settings.Default.LogLevel != "audit")
                return;

            _clientInfo.AddToLogGrid(message, LogType.Audit);
        }

        public override void Write(object o)
        {
            if (Settings.Default.LogLevel != "audit")
                return;

            _clientInfo.AddToLogGrid(o.ToString(), LogType.Audit);
        }

        public override void Write(string message, string category)
        {
            LogType logtype = GetLogTypeFromString(category);

            // Determine if we should log this or not.
            if (Settings.Default.LogLevel == "error" && logtype != LogType.Error)
                return;

            if (Settings.Default.LogLevel == "info" && (logtype != LogType.Error && logtype != LogType.Info))
                return;

            _clientInfo.AddToLogGrid(message, logtype);
        }

        public override void Write(object o, string category)
        {
            LogType logtype = GetLogTypeFromString(category);

            // Determine if we should log this or not.
            if (Settings.Default.LogLevel == "error" && logtype != LogType.Error)
                return;

            if (Settings.Default.LogLevel == "info" && (logtype != LogType.Error && logtype != LogType.Info))
                return;

            _clientInfo.AddToLogGrid(o.ToString(), logtype);
        }

        public override void WriteLine(string message)
        {
            Write(message + "\n");
        }

        public override void WriteLine(object o)
        {
            Write(o.ToString() + "\n");
        }

        public override void WriteLine(string message, string category)
        {
            Write((message + "\n"), category);
        }

        public override void WriteLine(object o, string category)
        {
            Write((o.ToString() + "\n"), category);
        }

        public override void Fail(string message)
        {
            // Dont write
        }

        public override void Fail(string message, string detailMessage)
        {
            // Dont write
        }
        #endregion
    }
}
