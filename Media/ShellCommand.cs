/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2012 Daniel Garner
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
using System.Text;
using System.Diagnostics;
using XiboClient.Properties;
using XiboClient.Logic;

namespace XiboClient
{
    class ShellCommand : Media
    {
        string _command = "";
        string _code = "";

        public ShellCommand(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _command = Uri.UnescapeDataString(options.Dictionary.Get("windowsCommand")).Replace('+', ' ');
            _code = options.Dictionary.Get("commandCode");
        }

        public override void RenderMedia()
        {
            if (!string.IsNullOrEmpty(_code))
            {
                // Stored command
                bool success;

                try
                {
                    Command command = Command.GetByCode(_code);
                    success = command.run();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Cannot run Command: " + e.Message), LogType.Error.ToString());
                    success = false;
                }

                // Notify the state of the command (success or failure)
                using (xmds.xmds statusXmds = new xmds.xmds())
                {
                    statusXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds;
                    statusXmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, "{\"lastCommandSuccess\":" + success + "}");
                }
            }
            else
            {
                // shell command

                // Is this module enabled?
                if (ApplicationSettings.Default.EnableShellCommands)
                {
                    // Check to see if we have an allow list
                    if (!string.IsNullOrEmpty(ApplicationSettings.Default.ShellCommandAllowList))
                    {
                        // Array of allowed commands
                        string[] allowedCommands = ApplicationSettings.Default.ShellCommandAllowList.Split(',');

                        // Check we are allowed to execute the command
                        bool found = false;

                        foreach (string allowedCommand in allowedCommands)
                        {
                            if (_command.StartsWith(allowedCommand))
                            {
                                found = true;
                                ExecuteShellCommand();
                                break;
                            }
                        }

                        if (!found)
                            Trace.WriteLine(new LogMessage("ShellCommand - RenderMedia", "Shell Commands not in allow list: " + ApplicationSettings.Default.ShellCommandAllowList), LogType.Error.ToString());
                    }
                    else
                    {
                        // All commands are allowed
                        ExecuteShellCommand();
                    }
                }
                else
                {
                    Trace.WriteLine(new LogMessage("ShellCommand - RenderMedia", "Shell Commands are disabled"), LogType.Error.ToString());
                }
            }

            // All shell commands have a duration of 1
            base.RenderMedia();
        }

        /// <summary>
        /// Execute the shell command
        /// </summary>
        private void ExecuteShellCommand()
        {
            Trace.WriteLine(new LogMessage("ShellCommand - ExecuteShellCommand", _command), LogType.Info.ToString());

            // Execute the commend
            if (!string.IsNullOrEmpty(_command))
            {
                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();

                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/C " + _command;

                    process.StartInfo = startInfo;
                    process.Start();
                }
            }
        }
    }
}
