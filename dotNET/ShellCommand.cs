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

namespace XiboClient
{
    class ShellCommand : Media
    {
        string _command = "";

        public ShellCommand(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _command = options.Dictionary.Get("windowsCommand");
        }

        public override void RenderMedia()
        {
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

            // All shell commands have a duration of 1
            base.RenderMedia();
        }
    }
}
