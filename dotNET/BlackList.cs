/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner and James Packer
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
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.Diagnostics;

namespace XiboClient
{
    class BlackList : IDisposable
    {
        private xmds.xmds xmds1;
        private HardwareKey hardwareKey;

        private string blackListFile;

        public BlackList()
        {
            // Check that the black list file is available
            blackListFile = Application.UserAppDataPath + "//" + Properties.Settings.Default.blackListLocation;

            // Get the key for this display
            hardwareKey = new HardwareKey();
        }

        /// <summary>
        /// Adds a media item to the Black list. Adds Locally and to the WebService
        /// </summary>
        /// <param name="id">The Media ID</param>
        /// <param name="type">The BlackListType, either All (to blacklist on all displays) or Single (to blacklist only on this display)</param>
        /// <param name="reason">The reason for the blacklist</param>
        public void Add(string id, BlackListType type, string reason)
        {
            // Do some validation
            if (reason == "") reason = "No reason provided";
            
            int mediaId;
            if (!int.TryParse(id, out mediaId))
            {
                System.Diagnostics.Trace.WriteLine(String.Format("Currently can only append Integer media types. Id {0}", id), "BlackList - Add");
            }

            // Send to the webservice
            xmds1 = new XiboClient.xmds.xmds();
            xmds1.BlackListCompleted += new XiboClient.xmds.BlackListCompletedEventHandler(xmds1_BlackListCompleted);

            xmds1.BlackListAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, mediaId, type.ToString(), reason, Properties.Settings.Default.Version);

            // Add to the local list
            AddLocal(id);
        }

        private void xmds1_BlackListCompleted(object sender, XiboClient.xmds.BlackListCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                System.Diagnostics.Trace.WriteLine("Error sending blacklist", "BlackList - BlackListCompleted");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("Blacklist sending complete", "BlackList - BlackListCompleted");
            }

            return;
        }

        /// <summary>
        /// Adds the Media Items in the XMLNodeList to the Blacklist (will only add these locally)
        /// </summary>
        /// <param name="items">The XMLNodeList containing the blacklist items. Each node must have an "id".</param>
        public void Add(XmlNodeList items)
        {
            Trace.WriteLine(new LogMessage("Blacklist - Add", "Adding XMLNodeList to Blacklist"), LogType.Info.ToString());

            foreach (XmlNode node in items)
            {
                XmlAttributeCollection attributes = node.Attributes;

                if (attributes["id"].Value != null)
                {
                    AddLocal(attributes["id"].Value);
                }
            }
        }

        /// <summary>
        /// Adds the Media ID to the local blacklist
        /// </summary>
        /// <param name="id">The ID to be blacklisted.</param>
        private void AddLocal(string id)
        {
            try
            {
                StreamWriter tw = new StreamWriter(File.Open(blackListFile, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                tw.Write(String.Format("[{0}],", id));
                tw.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "Blacklist - Add");
                System.Diagnostics.Trace.WriteLine(String.Format("Cant add {0} to the blacklist", id));
            }

            return;
        }

        /// <summary>
        /// Truncates the local Blacklist
        /// </summary>
        public void Truncate()
        {
            try
            {
                File.Delete(Application.UserAppDataPath + "//" + Properties.Settings.Default.blackListLocation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Cannot truncate the BlackList", "Blacklist - Truncate");
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Checks whether or not a media item is in the blacklist
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        public Boolean BlackListed(string fileId)
        {
            StreamReader sr = null;

            // Store as an XML Fragment
            if (!File.Exists(blackListFile))
            {
                return false;
            }

            try
            {
                // Use an XML Text Reader to grab the shiv from the black list location.
                sr = new StreamReader(File.Open(blackListFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                
                string listed = sr.ReadToEnd();

                return listed.Contains(String.Format("[{0}]", fileId));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "BlackList - BlackListed");
            }
            finally
            {
                // Make sure the xr is closed
                if (sr != null) sr.Close();
            }

            return false;
        }

        #region IDisposableMethods

        private Boolean disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources.
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    public enum BlackListType { Single, All }
}
