using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace XiboClient.Logic
{
    [Serializable]
    public class Command
    {
        public string Code;
        public string CommandString;
        public string Validation;

        public bool notifyStatus()
        {
            return !string.IsNullOrEmpty(Validation);
        }

        /// <summary>
        /// Run the Command
        /// </summary>
        /// <returns>true on success</returns>
        public bool Run()
        {
            if (string.IsNullOrEmpty(CommandString))
                throw new ArgumentNullException("Command string is empty, please check your Display Profile " + Code + " command for a valid command string.");

            // Parse the command string to work out how we should run this command.
            if (CommandString.StartsWith("rs232"))
            {
                Rs232Command rs232 = new Rs232Command(this);
                string line = rs232.Run();

                if (notifyStatus())
                {
                    return line == Validation;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                // Process with CMD
                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();

                    startInfo.CreateNoWindow = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/C " + CommandString;
                    startInfo.UseShellExecute = false;

                    if (notifyStatus())
                        startInfo.RedirectStandardOutput = true;

                    process.StartInfo = startInfo;
                    process.Start();

                    if (notifyStatus())
                    {
                        string line = "";
                        while (!process.StandardOutput.EndOfStream)
                        {
                            line += process.StandardOutput.ReadLine();
                        }

                        return line == Validation;
                    }
                    else
                        return true;
                }
            }
        }

        /// <summary>
        /// Get a command from Application Settings based on its Command Code
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static Command GetByCode(string code)
        {
            foreach (Command command in ApplicationSettings.Default.Commands)
            {
                if (command.Code == code)
                    return command;
            }

            throw new KeyNotFoundException("Command Not Found");
        }
    }
}
