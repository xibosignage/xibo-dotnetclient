/**
 * Copyright (C) 2019 Xibo Signage Ltd
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using XiboClient.Logic;

namespace XiboClient
{
    [Serializable()]
    public class ApplicationSettings
    {
        private static ApplicationSettings _instance;
        private static string _default = "default";
        private List<string> _globalProperties;

        // Application Specific Settings we want to protect
        private readonly string _clientVersion = "2 R201";
        private readonly string _version = "5";
        private readonly int _clientCodeVersion = 201;

        public string ClientVersion { get { return _clientVersion; } }
        public string Version { get { return _version; } }
        public int ClientCodeVersion { get { return _clientCodeVersion; } }

        private ApplicationSettings()
        {
            _globalProperties = new List<string>();
            _globalProperties.Add("ServerUri");
            _globalProperties.Add("ServerKey");
            _globalProperties.Add("LibraryPath");
            _globalProperties.Add("ProxyUser");
            _globalProperties.Add("ProxyPassword");
            _globalProperties.Add("ProxyDomain");
            _globalProperties.Add("ProxyPort");
        }

        /// <summary>
        /// Access the application settings.
        /// load()
        /// </summary>
        public static ApplicationSettings Default
        {
            get
            {
                // Return the settings instance if we've loaded already
                if (_instance != null)
                    return _instance;

                // Check to see if we need to migrate
                XmlDocument document;
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string fileName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);

                // Create a new application settings instance.
                _instance = new ApplicationSettings();

                if (File.Exists(path + Path.DirectorySeparatorChar + fileName + ".config.xml"))
                {
                    // Migrate to the new settings format
                    //  player specific settings in %APPDATA%\<app_name>.config.json
                    //  cms settings and run time info in <library>\config.json
                    try
                    {
                        // Load the XML document
                        document = new XmlDocument();
                        document.Load(path + Path.DirectorySeparatorChar + fileName + ".config.xml");

                        _instance.PopulateFromXml(document);
                        _instance.Save();
                    }
                    catch
                    {
                        // Unable to load XML - consider the migration complete
                    }

                    // Take a backup and Delete the old config file
                    File.Copy(path + Path.DirectorySeparatorChar + fileName + ".config.xml", path + Path.DirectorySeparatorChar + fileName + ".config.xml.bak", true);
                    File.Delete(path + Path.DirectorySeparatorChar + fileName + ".config.xml");
                }

                // Populate it with the default.config.xml
                _instance.AppendConfigFile(Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar + _default + ".config.xml");

                // Load the global settings.
                _instance.AppendConfigFile(path + Path.DirectorySeparatorChar + fileName + ".xml");

                // Load the hardware key
                if (File.Exists(_instance.LibraryPath + "\\hardwarekey"))
                {
                    _instance.HardwareKey = File.ReadAllText(_instance.LibraryPath + "\\hardwarekey");
                }

                // Load the player settings
                _instance.AppendConfigFile(_instance.LibraryPath + "\\config.xml");

                // Return the instance
                return _instance;
            }
        }

        /// <summary>
        /// Append config file
        /// </summary>
        /// <param name="path"></param>
        private void AppendConfigFile(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    XmlDocument document = new XmlDocument();
                    document.Load(path);
                    PopulateFromXml(document);
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("ApplicationSettings - AppendConfigFile", "Unable to load config file."), LogType.Error.ToString());
                }
            }
        }

        /// <summary>
        /// Save settings
        /// </summary>
        public void Save()
        {
            if (_instance == null)
                return;

            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fileName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);

            // Write the global settings file
            using (XmlWriter writer = XmlWriter.Create(path + Path.DirectorySeparatorChar + fileName + ".xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ApplicationSettings");

                foreach (PropertyInfo property in _instance.GetType().GetProperties())
                {
                    if (property.CanRead && _globalProperties.Contains(property.Name))
                    {
                        writer.WriteElementString(property.Name, "" + property.GetValue(_instance));
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            // Write the hardware key file
            File.WriteAllText(_instance.LibraryPath + "\\hardwarekey", _instance.HardwareKey);

            // Write the player settings file
            using (XmlWriter writer = XmlWriter.Create(_instance.LibraryPath + "\\config.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("PlayerSettings");

                foreach (PropertyInfo property in _instance.GetType().GetProperties())
                {
                    try
                    {
                        if (property.CanRead && !_globalProperties.Contains(property.Name) && property.Name != "HardwareKey")
                        {
                            if (property.Name == "Commands")
                            {
                                writer.WriteStartElement("commands");

                                foreach (Command command in _instance.Commands)
                                {
                                    writer.WriteStartElement(command.Code);
                                    writer.WriteElementString("commandString", command.CommandString);
                                    writer.WriteElementString("commandValidation", command.Validation);
                                    writer.WriteEndElement();
                                }

                                writer.WriteEndElement();
                            }
                            else
                            {
                                writer.WriteElementString(property.Name, "" + property.GetValue(_instance));
                            }
                        }
                    }
                    catch
                    {
                        Trace.WriteLine(new LogMessage("PopulateFromXml", "Unable to write [" + property.Name + "]."), LogType.Info.ToString());
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        /// <summary>
        /// Object array access
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Populate from the XML document provided.
        /// </summary>
        /// <param name="document"></param>
        public void PopulateFromXml(XmlDocument document)
        {
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                // Are we a commands node?
                if (node.Name.ToLower() == "commands")
                {
                    List<Command> commands = new List<Command>();

                    foreach (XmlNode commandNode in node.ChildNodes)
                    {
                        Command command = new Command();
                        command.Code = commandNode.Name;
                        command.CommandString = commandNode.SelectSingleNode("commandString").InnerText;
                        command.Validation = commandNode.SelectSingleNode("validationString").InnerText;

                        commands.Add(command);
                    }

                    // Store commands
                    ApplicationSettings.Default.Commands = commands;
                }
                else
                {
                    Object value = node.InnerText;
                    string type = (node.Attributes["type"] != null) ? node.Attributes["type"].Value : "string";

                    switch (type)
                    {
                        case "int":
                            value = Convert.ToInt32(value);
                            break;

                        case "double":
                            value = Convert.ToDecimal(value);
                            break;

                        case "string":
                        case "word":
                            value = node.InnerText;
                            break;

                        case "checkbox":
                            value = (node.InnerText == "0") ? false : true;
                            break;

                        default:
                            continue;
                    }

                    // Match these to settings
                    try
                    {
                        if (ApplicationSettings.Default[node.Name] != null)
                        {
                            value = Convert.ChangeType(value, ApplicationSettings.Default[node.Name].GetType());
                        }

                        ApplicationSettings.Default[node.Name] = value;
                    }
                    catch
                    {
                        Trace.WriteLine(new LogMessage("PopulateFromXml", "XML configuration for [" + node.Name + "] which this player doesn't understand."), LogType.Info.ToString());
                    }
                }
            }
        }

        #region "The Settings"
        public int XmdsResetTimeout { get; set; }

        public decimal SizeX { get; set; }
        public decimal SizeY { get; set; }
        public decimal OffsetX { get; set; }
        public decimal OffsetY { get; set; }
        public decimal EmptyLayoutDuration { get; set; }

        public bool EnableExpiredFileDeletion { get; set; }
        public bool ForceHttps { get; set; }

        public int LibraryAgentInterval { get; set; }

        public string ScheduleFile { get; set; }
        public string LogLocation { get; set; }
        public string StatsLogFile { get; set; }
        public string CacheManagerFile { get; set; }
        public string RequiredFilesFile { get; set; }
        public string VideoRenderingEngine { get; set; }
        public string NewCmsAddress { get; set; }
        public string NewCmsKey { get; set; }

        private string _libraryPath;
        public string LibraryPath 
        { 
            get 
            {
                if (_libraryPath == "DEFAULT")
                {
                    // Get the users document space for a library
                    _libraryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\" + Application.ProductName + " Library";
                }

                // Check the path exists
                if (!String.IsNullOrEmpty(_libraryPath) && !Directory.Exists(_libraryPath))
                {
                    // Create everything up to the folder name we've specified.
                    Directory.CreateDirectory(_libraryPath);
                }

                return _libraryPath;
            } 
            set 
            {
                _libraryPath = value; 
            } 
        }

        /// <summary>
        /// XMDS Url configuration
        /// </summary>
        public string XiboClient_xmds_xmds
        {
            get
            {
                return ServerUri.TrimEnd('\\') + @"/xmds.php?v=" + ApplicationSettings.Default.Version;
            }
        }
        
        public string ServerKey { get; set; }

        private string _displayName;
        public string DisplayName { get { return (_displayName == "COMPUTERNAME") ? Environment.MachineName : _displayName; } set { _displayName = value; } }

        /// <summary>
        /// Server Address
        /// </summary>
        private string _serverUri;
        public string ServerUri 
        { 
            get
            {
                return (string.IsNullOrEmpty(_serverUri)) ? "http://localhost" : _serverUri;
            }
            set
            {
                _serverUri = value;
            }
        }

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
        public string XmrNetworkAddress { get; set; }

        // Download window
        public string DisplayTimeZone { get; set; }
        public string DownloadStartWindow { get; set; }
        public string DownloadEndWindow { get; set; }

        // Embedded web server config
        public int EmbeddedServerPort { get; set; }
        public string EmbeddedServerAddress 
        { 
            get 
            {
                return "http://localhost:" + ((EmbeddedServerPort == 0) ? 9696 : EmbeddedServerPort) + "/";
            }
        }

        public DateTime DownloadStartWindowTime
        {
            get
            {
                return getDateFromHi(DownloadStartWindow);
            }
        }

        public DateTime DownloadEndWindowTime
        {
            get
            {
                return getDateFromHi(DownloadEndWindow);
            }
        }

        /// <summary>
        /// Get a locally formatted date based on the H:i string provided.
        /// </summary>
        /// <param name="hi"></param>
        /// <returns></returns>
        private DateTime getDateFromHi(string hi)
        {
            DateTime now = DateTime.Now;

            try
            {
                int h;
                int m;

                // Expect the format H:i (24 hour). If we don't have a : in it, then it is likely being fed by an old CMS, so disable
                if (!hi.Contains(":"))
                {
                    h = 0;
                    m = 0;
                }
                else
                {
                    string[] split = hi.Split(':');
                    h = int.Parse(split[0]);
                    m = int.Parse(split[1]);
                }

                return new DateTime(now.Year, now.Month, now.Day, h, m, 0, DateTimeKind.Local);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("getDateFromHi", "Unable to parse H:i, Error = " + e.Message), LogType.Info.ToString());
                return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Local);
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
                    if (DownloadStartWindow == DownloadEndWindow)
                        return true;

                    DateTime startWindow = DownloadStartWindowTime;
                    if (DownloadEndWindowTime < startWindow)
                        startWindow = DownloadStartWindowTime.AddDays(-1);

                    return (startWindow <= DateTime.Now && DownloadEndWindowTime >= DateTime.Now);
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
        public int ScreenShotSize { get; set; }

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
        public bool SendCurrentLayoutAsStatusUpdate { get; set; }
        public bool PreventSleep { get; set; }
        public bool ScreenShotRequested { get; set; }

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

        public List<Command> Commands { get; set; }

        #endregion

        // Settings HASH
        public string Hash { get; set; }
    }
}
