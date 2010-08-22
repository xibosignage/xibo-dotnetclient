/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2010 Daniel Garner
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
using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Diagnostics;

namespace XiboClient
{
    class ScheduleManager
    {
        private string _location;
        private Collection<LayoutSchedule> _layoutSchedule;
        private Collection<LayoutSchedule> _currentSchedule;
        private bool _refreshSchedule;

        public ScheduleManager(string scheduleLocation)
        {
            _location = scheduleLocation;

            // Create an empty layout schedule
            _layoutSchedule = new Collection<LayoutSchedule>();

            // Evaluate the Schedule
            EvaluateSchedule();
        }

        #region "Properties"

        /// <summary>
        /// Is there a new schedule available
        /// </summary>
        public bool NewScheduleAvailable
        {
            get
            {
                return EvaluateSchedule();
            }
        }

        /// <summary>
        /// Tell the schedule manager to Refresh the Schedule
        /// </summary>
        public bool RefreshSchedule
        {
            get
            {
                return _refreshSchedule;
            }
            set
            {
                _refreshSchedule = value;
            }
        }

        /// <summary>
        /// The current layout schedule
        /// </summary>
        public Collection<LayoutSchedule> CurrentSchedule
        {
            get
            {
                return _currentSchedule;
            }
        }

        #endregion

        
        /// <summary>
        /// Determine if there is a new schedule available
        /// </summary>
        /// <returns></returns>
        private bool EvaluateSchedule()
        {
            // If we dont currently have a cached schedule load one from the scheduleLocation
            // also do this if we have been told to Refresh the schedule
            if (_layoutSchedule.Count == 0 || RefreshSchedule)
            {
                _layoutSchedule = LoadScheduleFromFile();

                // Set RefreshSchedule to be false (this means we will not need to load the file constantly)
                RefreshSchedule = false;
            }
            
            // We need to build the current schedule from the layout schedule (obeying date/time)

            return false;
        }

        /// <summary>
        /// Loads the schedule from file.
        /// </summary>
        /// <returns></returns>
        private Collection<LayoutSchedule> LoadScheduleFromFile()
        {
            // Get the schedule XML
            XmlDocument scheduleXml = GetScheduleXml();

            // This function must either exit without doing anything (i.e. continue playing the current list of layouts)
            // OR repopulate the layoutSchedule collection with a new list of layouts and call change on the first one.

            // Get the layouts in this schedule
            XmlNodeList listLayouts = scheduleXml.SelectNodes("/schedule/layout");

            // Have we got some?
            if (listLayouts.Count == 0)
            {
                // Schedule up the default
                LayoutSchedule temp = new LayoutSchedule();
                temp.layoutFile = Properties.Settings.Default.LibraryPath + @"\Default.xml";
                temp.id = 0;
                temp.scheduleid = 0;

                _layoutSchedule.Add(temp);
            }
            else
            {
                // Get them and add to collection
                foreach (XmlNode layout in listLayouts)
                {
                    XmlAttributeCollection attributes = layout.Attributes;

                    string layoutFile = attributes["file"].Value;
                    string replace = ".xml";
                    layoutFile = layoutFile.TrimEnd(replace.ToCharArray());

                    string scheduleId = "";
                    if (attributes["scheduleid"] != null) scheduleId = attributes["scheduleid"].Value;

                    LayoutSchedule temp = new LayoutSchedule();
                    temp.layoutFile = Properties.Settings.Default.LibraryPath + @"\" + layoutFile + @".xlf";
                    temp.id = int.Parse(layoutFile);

                    if (scheduleId != "") temp.scheduleid = int.Parse(scheduleId);

                    _layoutSchedule.Add(temp);
                }
            }

            // Clean up
            listLayouts = null;
            scheduleXml = null;
        }

        /// <summary>
        /// Gets the Schedule XML
        /// </summary>
        /// <returns></returns>
        private XmlDocument GetScheduleXml()
        {
            XmlDocument scheduleXml;
            // Check the schedule file exists
            if (File.Exists(_location))
            {
                // Read the schedule file
                XmlReader reader = XmlReader.Create(_location);

                scheduleXml = new XmlDocument();
                scheduleXml.Load(reader);

                reader.Close();
            }
            else
            {
                // Use the default XML
                scheduleXml = new XmlDocument();
                scheduleXml.LoadXml("<schedule></schedule>");
            }
            return scheduleXml;
        }
    }

    /// <summary>
    /// A LayoutSchedule
    /// </summary>
    [Serializable]
    public struct LayoutSchedule
    {
        public string layoutFile;
        public int id;
        public int scheduleid;
    }
}
