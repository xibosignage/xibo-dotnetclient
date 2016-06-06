/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-16 Spring Signage Ltd
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
using System.Globalization;
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
        private string _clientVersion = "1.7.8";
        private string _version = "4";
        private int _clientCodeVersion = 112;

        public string ClientVersion { get { return _clientVersion; } }
        public string Version { get { return _version; } }
        public int ClientCodeVersion { get { return _clientCodeVersion; } }

        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);

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
                    if (File.Exists(path + Path.DirectorySeparatorChar + fileName + ".bak"))
                    {
                        if (File.Exists(path + Path.DirectorySeparatorChar + fileName))
                            File.Delete(path + Path.DirectorySeparatorChar + fileName);

                        File.Copy(path + Path.DirectorySeparatorChar + fileName + ".bak", path + Path.DirectorySeparatorChar + fileName);
                    }

                    MessageBox.Show(string.Format("Corrupted configuration file, will try to restore from backup. Please restart. Message: {0}. Path: {1}.", e.Message, path + Path.DirectorySeparatorChar + fileName));
                    throw;
                }
            }
        }

        public void Save()
        {
            if (_instance == null)
                return;

            XmlSerializer serial = new XmlSerializer(typeof(ApplicationSettings));
            
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                Path.DirectorySeparatorChar +
                Path.GetFileNameWithoutExtension(Application.ExecutablePath) + '.' + _fileSuffix;

            // Copy the old settings file to a backup
            if (File.Exists(path + ".bak"))
                File.Delete(path + ".bak");

            File.Copy(path, path + ".bak");

            // Serialise into a new file
            using (StreamWriter sr = new StreamWriter(path + ".new"))
            {
                serial.Serialize(sr, _instance);
            }

            // If we've done that successfully, then move the new file into the original
            if (File.Exists(path))
                File.Delete(path);

            File.Move(path + ".new", path);
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
        public double CmsTimeOffset { get; set; }

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

        private string _libraryPath;
        public string LibraryPath { get { return (_libraryPath == "DEFAULT") ? (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\" + Application.ProductName + " Library") : _libraryPath; } set { _libraryPath = value; } }
        public string XiboClient_xmds_xmds { get; set; }
        public string ServerKey { get; set; }

        private string _displayName;
        public string DisplayName { get { return (_displayName == "COMPUTERNAME") ? Environment.MachineName : _displayName; } set { _displayName = value; } }

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

        // Download window
        
        public long DownloadStartWindow { get; set; }
        public long DownloadEndWindow { get; set; }

        public DateTime DownloadStartWindowTime
        {
            get
            {
                // Get the local time now and add our Unix timestamp to it.
                // We know that the DownloadStartWindow is saved in UTC (GMT to be precise, but no biggie)
                DateTime now = DateTime.Now;
                DateTime start = unixEpoch.AddMilliseconds(DownloadStartWindow);

                // start is now UTC download window start.
                if (CmsTimeOffset != null && CmsTimeOffset != 0)
                {
                    // Adjust for the timezone
                    start = start.AddHours(CmsTimeOffset);
                }
                
                // Reset to local time, using the H:m:i from the Unix Time.
                // This gives us a local time
                return new DateTime(now.Year, now.Month, now.Day, start.Hour, start.Minute, start.Second, DateTimeKind.Local);
            }
        }

        public DateTime DownloadEndWindowTime
        {
            get
            {
                // See notes from DownloadStartWindowTime
                DateTime now = DateTime.Now;
                DateTime end = unixEpoch.AddMilliseconds(DownloadEndWindow);

                if (CmsTimeOffset != null && CmsTimeOffset != 0)
                {
                    // Adjust for the timezone
                    end = end.AddHours(CmsTimeOffset);
                }

                // Reset to today
                return new DateTime(now.Year, now.Month, now.Day, end.Hour, end.Minute, end.Second, DateTimeKind.Local);
            }
        }

        /// <summary>
        /// Is the player in the download window
        /// </summary>
        public bool InDownloadWindow
        {
            get
            {
                try
                {
                    if (DownloadStartWindow == 0 && DownloadEndWindow == 0)
                        return true;

                    return (DownloadStartWindowTime <= DateTime.Now && DownloadEndWindowTime >= DateTime.Now);
                }
                catch
                {
                    return true;
                }
            }
        }

        public int Licensed { get; set; }
        public int StatsFlushCount { get; set; }
        public int CollectInterval { get; set; }
        public int MaxConcurrentDownloads { get; set; }
        public int ScreenShotRequestInterval { get; set; }

        private int _maxLogFileUploads;
        public int MaxLogFileUploads { get { return ((_maxLogFileUploads == 0) ? 10 : _maxLogFileUploads); } set { _maxLogFileUploads = value; } }

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
        public bool PreventSleep { get; set; }

        private bool _screenShotRequested = false;
        public bool ScreenShotRequested 
        {
            get
            {
                return _screenShotRequested;
            }
            set
            {
                _screenShotRequested = value;
                // Reset the Hash so that the next update is taken into account.
                Hash = "0";
            }
        }

        // XMDS Status Flags
        private DateTime _xmdsLastConnection;
        public DateTime XmdsLastConnection { get { return _xmdsLastConnection; } set { _xmdsErrorCountSinceSuccessful = 0; _xmdsLastConnection = value; } }
        private int _xmdsErrorCountSinceSuccessful = 0;
        public int XmdsErrorCountSinceSuccessful { get { return _xmdsErrorCountSinceSuccessful; } }

        public decimal XmdsCollectionIntervalFactor()
        {
            if (XmdsErrorCountSinceSuccessful == 0)
                return 1;

            return (XmdsErrorCountSinceSuccessful > 10) ? 5 : XmdsErrorCountSinceSuccessful / 2;
        }

        public void IncrementXmdsErrorCount()
        {
            lock (this)
            {
                _xmdsErrorCountSinceSuccessful++;
            };
        }

        // Settings HASH
        public string Hash { get; set; }
    }
}
