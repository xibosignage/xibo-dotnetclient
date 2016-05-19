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
            string path = Path.GetDirectoryName(Application.ExecutablePath) + @"\watchdog\x86\XiboClientWatchdog.exe";
            string args = "-p \"" + Application.ExecutablePath + "\" -l \"" + ApplicationSettings.Default.LibraryPath + "\"";

            // Start it
            if (File.Exists(path))
            {
                try
                {
                    Process.Start(path, args);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("WatchDogManager - Start", "Unable to start: " + e.Message), LogType.Error.ToString());
                }
            }
        }
    }
}
