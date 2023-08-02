/**
 * Copyright (C) 2023 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - http://www.xibo.org.uk
 *
 * This file is part of Xibo.
 *
 * Xibo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * Xibo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with Xibo.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Shapes;

namespace XiboClient.Action
{
    [Serializable]
    public class Command
    {
        public string Code;
        public string CommandString;
        public string Validation;

        /// <summary>
        /// Does this command use a helper?
        /// </summary>
        /// <returns></returns>
        public bool IsUsesHelper()
        {
            return CommandString.StartsWith("rs232")
                || CommandString == "SoftRestart"
                || CommandString.StartsWith("http|");
        }

        /// <summary>
        /// Is validation required?
        /// </summary>
        /// <returns></returns>
        public bool IsValidationRequired()
        {
            return !string.IsNullOrEmpty(Validation);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool IsValid(string value)
        {
            LogMessage.Audit("Command", "IsValid", "Testing if " + Code + " is valid, output to test is [" + value + "]");

            // Do we need to validate?
            if (IsValidationRequired())
            {
                // Is the validation string a regex.
                try
                {
                    Match match = Regex.Match(value, Validation);
                    return match.Success;
                }
                catch
                {
                    // Fallback to a string comparison
                    return value.Contains(Validation);
                }
            }
            else
            {
                return true;
            }
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

                return IsValid(line);
            }
            else if (CommandString == "SoftRestart")
            {
                // Call close.
                Application.Current.Dispatcher.Invoke(new System.Action(() => {
                    Application.Current.Shutdown();
                }));
                
                return true;
            }
            else if (CommandString.StartsWith("http|"))
            {
                HttpCommand command = new HttpCommand(this);
                var httpStatus = command.RunAsync();

                return IsValid(httpStatus.Result + "");
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
                    startInfo.RedirectStandardOutput = true;

                    process.StartInfo = startInfo;
                    process.Start();
                    process.Exited += Process_Exited;

                    string line = "";
                    while (!process.StandardOutput.EndOfStream)
                    {
                        line += process.StandardOutput.ReadLine();
                    }

                    return IsValid(line);
                }
            }
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            int exitCode = ((Process)sender).ExitCode;
            if (exitCode != 0)
            {
                LogMessage.Audit("Command", "Run", "Non-zero exit code [" + exitCode + "] returned for command " + Code);
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
