/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2011 Daniel Garner
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
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace XiboClient
{
    public class RequiredFiles
    {
        private XmlDocument _requiredFilesXml;
        public Collection<RequiredFile> _requiredFiles;
        private xmds.xmds _report;

        public RequiredFiles()
        {
            _requiredFiles = new Collection<RequiredFile>();

            // Create a webservice call
            _report = new XiboClient.xmds.xmds();

            // Start up the Xmds Service Object
            _report.Credentials = null;
            _report.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
            _report.UseDefaultCredentials = false;
        }

        /// <summary>
        /// Set required files from the XML document
        /// </summary>
        private void SetRequiredFiles()
        {
            // Itterate through the RF XML and populate the RF collection
            XmlNodeList fileNodes = _requiredFilesXml.SelectNodes("/files/file");

            foreach (XmlNode file in fileNodes)
            {
                RequiredFile rf = new RequiredFile(); 
                
                XmlAttributeCollection attributes = file.Attributes;

                rf.FileType = attributes["type"].Value;
                rf.Complete = 0;
                rf.Md5 = "";
                rf.LastChecked = DateTime.Now;

                if (rf.FileType == "media")
                {
                    string[] filePart = attributes["path"].Value.Split('.');
                    rf.Id = int.Parse(filePart[0]);
                    rf.Path = attributes["path"].Value;
                }
                else if (rf.FileType == "layout")
                {
                    rf.Id = int.Parse(attributes["path"].Value);
                    rf.Path = attributes["path"].Value + ".xlf";
                }
                else
                {
                    continue;
                }

                _requiredFiles.Add(rf);
            }
        }

        /// <summary>
        /// Required Files XML
        /// </summary>
        public XmlDocument RequiredFilesXml
        {
            set
            {
                _requiredFilesXml = value;
                SetRequiredFiles();
            }
        }

        /// <summary>
        /// Mark a RequiredFile as complete
        /// </summary>
        /// <param name="id"></param>
        /// <param name="md5"></param>
        public void MarkComplete(int id, string md5)
        {
            foreach (RequiredFile rf in _requiredFiles)
            {
                if (rf.Id == id)
                {
                    RequiredFile newRf = rf;

                    newRf.Complete = 1;
                    newRf.Md5 = md5;


                    _requiredFiles.Add(newRf);
                    _requiredFiles.Remove(rf);

                    return;
                }
            }
        }

        /// <summary>
        /// Mark a RequiredFile as incomplete
        /// </summary>
        /// <param name="id"></param>
        /// <param name="md5"></param>
        public void MarkIncomplete(int id, string md5)
        {
            foreach (RequiredFile rf in _requiredFiles)
            {
                if (rf.Id == id)
                {
                    RequiredFile newRf = rf;

                    newRf.Complete = 0;
                    newRf.Md5 = md5;

                    _requiredFiles.Add(newRf);
                    _requiredFiles.Remove(rf);

                    return;
                }
            }
        }

        /// <summary>
        /// Writes Required Files to disk
        /// </summary>
        public void WriteRequiredFiles()
        {
            Debug.WriteLine(new LogMessage("RequiredFiles - WriteRequiredFiles", "About to Write RequiredFiles"), LogType.Info.ToString());

            try
            {
                using (StreamWriter streamWriter = new StreamWriter(Application.UserAppDataPath + "\\" + Properties.Settings.Default.RequiredFilesFile))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(RequiredFiles));

                    xmlSerializer.Serialize(streamWriter, this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(new LogMessage("RequiredFiles - WriteRequiredFiles", "Unable to write RequiredFiles to disk because: " + ex.Message));
            }
        }

        /// <summary>
        /// Report Required Files to XMDS
        /// </summary>
        public void ReportInventory()
        {
            HardwareKey hardwareKey = new HardwareKey();

            // Build the XML required by media file
            string xml = "";
            
            foreach (RequiredFile rf in _requiredFiles)
            {
                xml += string.Format("<file type=\"{0}\" id=\"{1}\" complete=\"{2}\" lastChecked=\"{3}\" md5=\"{4}\" />", 
                    rf.FileType, rf.Id.ToString(), rf.Complete.ToString(), rf.LastChecked.ToString(), rf.Md5);
            }

            xml = string.Format("<files macAddress=\"{1}\">{0}</files>", xml, hardwareKey.MacAddress);

            _report.MediaInventoryAsync(Properties.Settings.Default.Version, Properties.Settings.Default.ServerKey,
                hardwareKey.Key, xml);
        }
    }

    public struct RequiredFile
    {
        public string FileType;
        public int Id;
        public int Complete;
        public DateTime LastChecked;
        public string Md5;
        public string Path;
    }
}
