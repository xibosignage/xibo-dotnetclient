using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace XiboClient.Log
{
    public sealed class ClientInfo
    {
        private static readonly Lazy<ClientInfo>
            lazy =
            new Lazy<ClientInfo>
            (() => new ClientInfo());

        public static ClientInfo Instance { get { return lazy.Value; } }

        /// <summary>
        /// Set the schedule status
        /// </summary>
        public string ScheduleStatus;

        /// <summary>
        /// Set the required files status
        /// </summary>
        public string RequiredFilesStatus;

        /// <summary>
        /// Set the schedule manager status
        /// </summary>
        public string ScheduleManagerStatus;

        /// <summary>
        /// Current Layout Id
        /// </summary>
        public string CurrentLayoutId;

        /// <summary>
        /// XMR Status
        /// </summary>
        public string XmrSubscriberStatus;

        /// <summary>
        /// Control Count
        /// </summary>
        public int ControlCount;

        /// <summary>
        /// The title
        /// </summary>
        private string Title;

        /// <summary>
        /// Client Info Object
        /// </summary>
        private ClientInfo()
        {
            // Put the XMDS url on the title window
            Title = "Player Information and Status - " + ApplicationSettings.Default.ServerUri;
        }

        /// <summary>
        /// Sets the currently playing layout name
        /// </summary>
        /// <param name="layoutName"></param>
        public void SetCurrentlyPlaying(string layoutName)
        {
            Title = "Client Information and Status - " + ApplicationSettings.Default.ServerUri + " - Currently Showing: " + layoutName;
        }

        /// <summary>
        /// Adds a log message
        /// </summary>
        /// <param name="message"></param>
        public void AddToLogGrid(string message, LogType logType)
        {
            /*if (InvokeRequired)
            {
                BeginInvoke(new AddLogMessage(AddToLogGrid), new object[] { message, logType });
                return;
            }

            // Prevent the log grid getting too large (clear at 500 messages)
            if (logDataGridView.RowCount > 500)
                logDataGridView.Rows.Clear();

            int newRow = logDataGridView.Rows.Add();

            LogMessage logMessage;

            try
            {
                logMessage = new LogMessage(message);
            }
            catch
            {
                logMessage = new LogMessage("Unknown", message);
            }

            logDataGridView.Rows[newRow].Cells[0].Value = logMessage._thread;
            logDataGridView.Rows[newRow].Cells[1].Value = logMessage.LogDate.ToString();
            logDataGridView.Rows[newRow].Cells[2].Value = logType.ToString();
            logDataGridView.Rows[newRow].Cells[3].Value = logMessage._method;
            logDataGridView.Rows[newRow].Cells[4].Value = logMessage._message;*/
        }

        /// <summary>
        /// Update the required files text box
        /// </summary>
        public void UpdateRequiredFiles(string requiredFilesString)
        {
            RequiredFilesStatus = requiredFilesString;
        }

        /// <summary>
        /// Update Status Marker File
        /// </summary>
        public void UpdateStatusMarkerFile()
        {
            try
            {
                File.WriteAllText(Path.Combine(ApplicationSettings.Default.LibraryPath, "status.json"),
                    "{\"lastActivity\":\"" + DateTime.Now.ToString() + "\",\"state\":\"" + Thread.CurrentThread.ThreadState.ToString() + "\",\"xmdsLastActivity\":\"" + ApplicationSettings.Default.XmdsLastConnection.ToString() + "\",\"xmdsCollectInterval\":\"" + ApplicationSettings.Default.CollectInterval.ToString() + "\"}");
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("ClientInfo - updateStatusFile", "Failed to update status file. e = " + e.Message), LogType.Error.ToString());
            }
        }

        public void notifyStatusToXmds()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);

                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    writer.Formatting = Formatting.None;
                    writer.WriteStartObject();
                    writer.WritePropertyName("lastActivity");
                    writer.WriteValue(DateTime.Now.ToString());
                    writer.WritePropertyName("applicationState");
                    writer.WriteValue(Thread.CurrentThread.ThreadState.ToString());
                    writer.WritePropertyName("xmdsLastActivity");
                    writer.WriteValue(ApplicationSettings.Default.XmdsLastConnection.ToString());
                    writer.WritePropertyName("scheduleStatus");
                    writer.WriteValue(ScheduleManagerStatus);
                    writer.WritePropertyName("requiredFilesStatus");
                    writer.WriteValue(RequiredFilesStatus);
                    writer.WritePropertyName("xmrStatus");
                    writer.WriteValue(XmrSubscriberStatus);
                    writer.WriteEndObject();
                }

                // Notify the state of the command (success or failure)
                using (xmds.xmds statusXmds = new xmds.xmds())
                {
                    statusXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
                    statusXmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, sb.ToString());
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("ClientInfo - notifyStatusToXmds", "Failed to notify status to XMDS. e = " + e.Message), LogType.Error.ToString());
            }
        }
    }
}