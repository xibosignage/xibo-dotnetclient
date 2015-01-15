/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-14 Spring Signage Ltd
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace XiboClient
{
    [Serializable()]
    public class ApplicationSettings
    {
        private static ApplicationSettings _instance;
        private static string _fileSuffix = "config.xml";
        private static string _default = "default";

        // Application Specific Settings we want to protect
        private string _clientVersion = "1.7.0-rc1";
        private string _version = "4";
        private int _clientCodeVersion = 103;

        public string ClientVersion { get { return _clientVersion; } }
        public string Version { get { return _version; } }
        public int ClientCodeVersion { get { return _clientCodeVersion; } }

        public static ApplicationSettings Default
        {
            get
            {
                if (_instance != null)
                    return _instance;

                string fileName = "";
                string path = "";

                try
                {
                    XmlSerializer serial = new XmlSerializer(typeof(ApplicationSettings));

                    path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    fileName = Path.GetFileNameWithoutExtension(Application.ExecutablePath) + '.' + _fileSuffix;

                    // The default config file is stored in the application executable path (install folder)
                    // with the default.config.xml suffix
                    string defaultConfigFile = Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar + _default + "." + _fileSuffix;

                    if (!File.Exists(path + Path.DirectorySeparatorChar + fileName))
                    {
                        // Copy the defaults
                        if (!File.Exists(defaultConfigFile))
                            throw new Exception("Missing default.xml file - this should be in the same path as the executable.");

                        File.Copy(defaultConfigFile, path + Path.DirectorySeparatorChar + fileName);
                    }

                    using (StreamReader sr = new StreamReader(path + Path.DirectorySeparatorChar + fileName))
                    {
                        ApplicationSettings appSettings = (ApplicationSettings)serial.Deserialize(sr);
                        return _instance = appSettings;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(string.Format("{0}. Path: {1}.", e.Message, path + Path.DirectorySeparatorChar + fileName));
                    throw;
                }
            }
        }

        public void Save()
        {
            if (_instance == null)
                return;

            XmlSerializer serial = new XmlSerializer(typeof(ApplicationSettings));
            string fileName = Path.GetFileNameWithoutExtension(Application.ExecutablePath) + '.' + _fileSuffix;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            using (StreamWriter sr = new StreamWriter(path + Path.DirectorySeparatorChar + fileName))
            {
                serial.Serialize(sr, _instance);
            }
        }

        public object this[string propertyName]
        {
            get
            {
                PropertyInfo property = GetType().GetProperty(propertyName);
                return property.GetValue(this, null);
            }
            set
            {
                PropertyInfo property = GetType().GetProperty(propertyName);
                property.SetValue(this, value, null);
            }
        }

        public int XmdsResetTimeout { get; set; }

        public decimal SizeX { get; set; }
        public decimal SizeY { get; set; }
        public decimal OffsetX { get; set; }
        public decimal OffsetY { get; set; }
        public decimal EmptyLayoutDuration { get; set; }

        public bool EnableExpiredFileDeletion { get; set; }

        public int LibraryAgentInterval { get; set; }

        public string ScheduleFile { get; set; }
        public string LogLocation { get; set; }
        public string StatsLogFile { get; set; }
        public string CacheManagerFile { get; set; }
        public string RequiredFilesFile { get; set; }
        public string VideoRenderingEngine { get; set; }

        public string LibraryPath { get; set; }
        public string XiboClient_xmds_xmds { get; set; }
        public string ServerKey { get; set; }
        public string DisplayName { get; set; }
        public string ServerUri { get; set; }
        public string ProxyUser { get; set; }
        public string ProxyPassword { get; set; }
        public string ProxyDomain { get; set; }
        public string ProxyPort { get; set; }
        public string BlackListLocation { get; set; }
        public string HardwareKey { get; set; }
        public string LogLevel { get; set; }
        public string SplashOverride { get; set; }
        public string ShellCommandAllowList { get; set; }
        public string LogToDiskLocation { get; set; }
        public string CursorStartPosition { get; set; }
        public string ClientInformationKeyCode { get; set; }

        public int Licensed { get; set; }
        public int StatsFlushCount { get; set; }
        public int CollectInterval { get; set; }
        public int MaxConcurrentDownloads { get; set; }
        public int ScreenShotRequestInterval { get; set; }

        public bool PowerpointEnabled { get; set; }
        public bool StatsEnabled { get; set; }
        public bool ExpireModifiedLayouts { get; set; }
        public bool EnableMouse { get; set; }
        public bool DoubleBuffering { get; set; }
        public bool EnableShellCommands { get; set; }
        public bool ShowInTaskbar { get; set; }
        public bool ClientInfomationCtrlKey { get; set; }
        public bool UseCefWebBrowser { get; set; }
        public bool SendCurrentLayoutAsStatusUpdate { get; set; }
        public bool ScreenShotRequested { get; set; }

        public DateTime XmdsLastConnection { get; set; }
    }
}
