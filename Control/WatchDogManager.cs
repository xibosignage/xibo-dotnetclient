using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace XiboClient.Control
{
    class WatchDogManager
    {
        public static void Start()
        {
            // Check to see if the WatchDog EXE exists where we expect it to be
            // Uncomment to test local watchdog install. 
            //string path = @"C:\Program Files (x86)\Xibo Player\watchdog\x86\XiboClientWatchdog.exe";
            string path = Path.GetDirectoryName(Application.ExecutablePath) + @"\watchdog\x86\XiboClientWatchdog.exe";
            string args = "-p \"" + Application.ExecutablePath + "\" -l \"" + ApplicationSettings.Default.LibraryPath + "\"";

            // Start it
            if (File.Exists(path))
            {
                try
                {
                    Process process = new Process();
                    ProcessStartInfo info = new ProcessStartInfo();

                    info.CreateNoWindow = true;
                    info.WindowStyle = ProcessWindowStyle.Hidden;
                    info.FileName = "cmd.exe";
                    info.Arguments = "/c start \"watchdog\" \"" + path + "\" " + args;

                    process.StartInfo = info;
                    process.Start();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("WatchDogManager - Start", "Unable to start: " + e.Message), LogType.Error.ToString());
                }
            }
        }
    }
}
