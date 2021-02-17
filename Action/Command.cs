/**
 * Copyright (C) 2021 Xibo Signage Ltd
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
using System.Windows;

namespace XiboClient.Action
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
            else if (CommandString == "SoftRestart")
            {
                // Call close.
                Application.Current.Dispatcher.Invoke(new System.Action(() => {
                    Application.Current.MainWindow.Close();
                }));
                
                return true;
            }
            else if (CommandString.StartsWith("http|"))
            {
                HttpCommand command = new HttpCommand(this);
                var httpStatus = command.RunAsync();

                if (notifyStatus())
                {
                    return httpStatus.Result + "" == Validation;
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
