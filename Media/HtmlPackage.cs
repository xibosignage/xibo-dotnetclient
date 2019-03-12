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
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XiboClient
{
    class HtmlPackage : IeWebMedia
    {
        public HtmlPackage(RegionOptions options)
            : base(options)
        {
            string pathToMediaFile = Path.Combine(ApplicationSettings.Default.LibraryPath, options.uri);
            string pathToPackageFolder = Path.Combine(ApplicationSettings.Default.LibraryPath, "package_" + options.FileId);
            string pathToStatusFile = Path.Combine(pathToPackageFolder, "_updated");

            // Configure the file path to indicate which file should be opened by the browser
            _filePath = ApplicationSettings.Default.EmbeddedServerAddress + "package_" + options.FileId + "/" + options.Dictionary.Get("nominatedFile", "index.html");

            // Check to see if our package has been extracted already
            // if not, then extract it
            if (!(Directory.Exists(pathToPackageFolder) && IsUpdated(pathToStatusFile, File.GetLastWriteTime(pathToMediaFile))))
            {
                // Extract our file into the specified folder.
                ZipFile.ExtractToDirectory(pathToMediaFile, pathToPackageFolder);

                // Add in our extraction date.
                WriteUpdatedFlag(pathToStatusFile);
            }
        }

        protected override bool IsNativeOpen()
        {
            return true;
        }

        /// <summary>
        /// Updated Flag
        /// </summary>
        /// <param name="path"></param>
        private void WriteUpdatedFlag(string path)
        {
            try
            {
                File.WriteAllText(path, DateTime.Now.ToString());
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HtmlPackage", "WriteUpdatedFlag: Failed to update status file: " + path + ". e = " + e.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Check whether we've updated recently
        /// </summary>
        /// <param name="path"></param>
        /// <param name="lastModified"></param>
        /// <returns></returns>
        private bool IsUpdated(string path, DateTime lastModified)
        {
            // Check that it is up to date by looking for our special file.
            try
            {
                string flag = File.ReadAllText(path);
                DateTime updated = DateTime.Parse(flag);

                return updated > lastModified;
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HtmlPackage", "IsUpdated: Failed to read status file: " + path + ". e = " + e.Message), LogType.Error.ToString());
                return false;
            }
        }
    }
}
