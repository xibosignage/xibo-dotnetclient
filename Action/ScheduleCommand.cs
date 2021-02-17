using System;
using System.Diagnostics;

namespace XiboClient.Action
{
    public class ScheduleCommand
    {
        public DateTime Date { get; set; }
        public string Code { get; set; }
        public Command Command { get; set; }
        public int ScheduleId { get; set; }

        private bool _run = false;
        public bool HasRun
        {
            get
            {
                return _run;
            }
            set
            {
                _run = value;
            }
        }

        public void Run()
        {
            bool success;

            try
            {
                // Get a fresh command from settings
                Command = Command.GetByCode(Code);

                // Run the command.
                success = Command.Run();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("CommandSchedule - Run", "Cannot start Run Command: " + e.Message), LogType.Error.ToString());
                success = false;
            }

            // Notify the state of the command (success or failure)
            using (xmds.xmds statusXmds = new xmds.xmds())
            {
                statusXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
                statusXmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, "{\"lastCommandSuccess\":" + success + "}");
            }
        }
    }
}
