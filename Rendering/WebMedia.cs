/**
 * Copyright (C) 2020 Xibo Signage Ltd
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
using EmbedIO.Utilities;
using Ionic.Zip;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace XiboClient.Rendering
{
    abstract class WebMedia : Media
    {
        /// <summary>
        /// The file path
        /// </summary>
        protected string _filePath;

        /// <summary>
        /// The local web file path
        /// </summary>
        protected string _localWebPath;

        /// <summary>
        /// Count of times DocumentComplete has been called
        /// </summary>
        private int _documentCompletedCount = 0;

        /// <summary>
        /// Reload on Xmds Refresh or not.
        /// </summary>
        private bool _reloadOnXmdsRefresh = false;

        /// <summary>
        /// A string to trigger on page load error
        /// </summary>
        protected string PageLoadErrorTrigger;

        // Events
        public delegate void HtmlUpdatedDelegate(string url);
        public event HtmlUpdatedDelegate HtmlUpdatedEvent;

        /// <summary>
        /// Lock for packaged HTML generation
        /// </summary>
        private static object _packageHtmlLock = new object();

        // Class Methods

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public WebMedia(MediaOptions options)
            : base(options)
        {
            // Set the file path/local web path
            if (IsNativeOpen())
            {
                // If we are modeid == 1, then just open the webpage without adjusting the file path
                _filePath = Uri.UnescapeDataString(options.uri).Replace('+', ' ');

                // Do we have a page load error trigger?
                PageLoadErrorTrigger = options.Dictionary.Get("pageLoadErrorTrigger");
            }
            else
            {
                // Set the file path
                _filePath = ApplicationSettings.Default.LibraryPath + @"\" + options.mediaid + ".htm";
                _localWebPath = ApplicationSettings.Default.EmbeddedServerAddress + options.mediaid + ".htm";
            }
        }

        /// <summary>
        /// Is this a native open widget
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsNativeOpen()
        {
            string modeId = Options.Dictionary.Get("modeid");
            return modeId != string.Empty && modeId == "1";
        }

        /// <summary>
        /// Configure this web media for use as a HTML package
        /// </summary>
        public void ConfigureForHtmlPackage()
        {
            // Force native rendering
            Options.Dictionary.Replace("modeid", "1");

            string pathToMediaFile = Path.Combine(ApplicationSettings.Default.LibraryPath, Options.uri);
            string pathToPackageFolder = Path.Combine(ApplicationSettings.Default.LibraryPath, "package_" + Options.FileId);
            string pathToStatusFile = Path.Combine(pathToPackageFolder, "_updated");
            string nominatedFile = Uri.UnescapeDataString(Options.Dictionary.Get("nominatedFile", "index.html")).Replace('+', ' ');

            // Configure the file path to indicate which file should be opened by the browser
            _filePath = ApplicationSettings.Default.EmbeddedServerAddress + "package_" + Options.FileId + "/" + nominatedFile;

            // Check to see if our package has been extracted already
            // if not, then extract it
            lock (_packageHtmlLock)
            {
                // if the directory doesn't yet exist, or if the status file has been updated. extract.
                if (!Directory.Exists(pathToPackageFolder) || IsUpdated(pathToStatusFile, File.GetLastWriteTime(pathToMediaFile)))
                {
                    using (ZipFile archive = new ZipFile(pathToMediaFile))
                    {
                        // Extract our file into the specified folder.
                        archive.ExtractAll(pathToPackageFolder, ExtractExistingFileAction.OverwriteSilently);
                    }
                    
                    // Add in our extraction date.
                    WriteUpdatedFlag(pathToStatusFile);
                }
            }
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

                Debug.WriteLine("Last time we extracted: " + updated.ToString() + ", ZIP file last modified: " + lastModified.ToString());

                return updated < lastModified;
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HtmlPackage", "IsUpdated: Failed to read status file: " + path + ". e = " + e.Message), LogType.Error.ToString());
                return false;
            }
        }

        /// <summary>
        /// Web Browser finished loading document
        /// </summary>
        protected void DocumentCompleted()
        {
            _documentCompletedCount++;

            // Prevent double document completions
            if (_documentCompletedCount > 1)
                return;

            // Start the timer
            base.RestartTimer();
        }

        /// <summary>
        /// Is the cached HTML ready
        /// </summary>
        /// <returns>true if there is something to show, false if nothing</returns>
        protected bool HtmlReady()
        {
            // Check for cached resource files in the library
            // We want to check the file exists first
            if (!File.Exists(_filePath))
            {
                // File doesn't exist at all.
                _reloadOnXmdsRefresh = true;

                // Refresh
                RefreshFromXmds();

                // Return false
                return false;
            }

            // It exists - therefore we want to get the last time it was updated
            DateTime lastWriteDate = File.GetLastWriteTime(_filePath);

            // Does it update every time?
            if (Options.updateInterval == 0)
            {
                // Comment in to force a re-request with each reload of the widget
                //_reloadOnXmdsRefresh = true;

                // File exists but needs updating
                RefreshFromXmds();

                // Comment in to force a re-request with each reload of the widget
                //return false;
            }
            // Compare the last time it was updated to the layout modified time (always refresh when the layout has been modified)
            // Also compare to the update interval (refresh if it has not been updated for longer than the update interval)
            else if (Options.LayoutModifiedDate.CompareTo(lastWriteDate) > 0 || DateTime.Now.CompareTo(lastWriteDate.AddMinutes(Options.updateInterval)) > 0)
            {
                // File exists but needs updating.
                RefreshFromXmds();
            }
            else
            {
                // File exists and is in-date - nothing to do                
            }

            // Refresh the local file cache with any new dimensions, etc.
            UpdateCacheIfNecessary();

            return true;
        }

        /// <summary>
        /// Pulls the duration out of the temporary file and sets the media Duration to the same
        /// </summary>
        protected void ReadControlMeta()
        {
            // read the contents of the file
            using (StreamReader reader = new StreamReader(_filePath))
            {
                string html = reader.ReadToEnd();

                // Parse out the duration using a regular expression
                try
                {
                    Match match = Regex.Match(html, "<!-- DURATION=(.*?) -->");

                    if (match.Success)
                    {
                        // We have a match, so override our duration.
                        Duration = Convert.ToInt32(match.Groups[1].Value);
                    }
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("Html - ReadControlMeta", "Unable to pull duration using RegEx").ToString());
                }
            }
        }

        /// <summary>
        /// Refresh the Local cache of the DataSetView HTML
        /// </summary>
        private void RefreshFromXmds()
        {
            xmds.xmds xmds = new XiboClient.xmds.xmds();
            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=getResource";
            xmds.GetResourceCompleted += new XiboClient.xmds.GetResourceCompletedEventHandler(xmds_GetResourceCompleted);

            xmds.GetResourceAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, Options.layoutId, Options.regionId, Options.mediaid, ApplicationSettings.Default.Version);
        }

        /// <summary>
        /// Refresh Complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void xmds_GetResourceCompleted(object sender, XiboClient.xmds.GetResourceCompletedEventArgs e)
        {
            try
            {
                // Success / Failure
                if (e.Error != null)
                {
                    Trace.WriteLine(new LogMessage("xmds_GetResource", "Unable to get Resource: " + e.Error.Message), LogType.Info.ToString());

                    // We have failed to update from XMDS
                    // id we have been asked to reload on XmdsRefresh, check to see if we have a file to load,
                    // if not expire on a short timer.
                    if (_reloadOnXmdsRefresh)
                    {
                        if (File.Exists(_filePath))
                        {
                            // Cached file to revert to
                            UpdateCacheIfNecessary();

                            // Navigate to the file
                            HtmlUpdatedEvent?.Invoke(_localWebPath);
                        }
                        else
                        {
                            // No cache to revert to
                            Trace.WriteLine(new LogMessage("xmds_GetResource", "We haven't been able to download this widget and there isn't a pre-cached one to use. Skipping."), LogType.Info.ToString());

                            // Start the timer so that we expire
                            Duration = 2;
                            base.RenderMedia(0);
                        }
                    }
                }
                else
                {
                    // Ammend the resource file so that we can open it directly from the library (this is better than using a tempoary file)
                    string cachedFile = e.Result;

                    // Handle the background
                    string html = MakeHtmlSubstitutions(cachedFile);

                    // Comment in to write out the update date at the end of the file (in the body)
                    // This is useful if you want to check how frequently the file is updating
                    //html = html.Replace("<body>", "<body><h1 style='color:white'>" + DateTime.Now.ToString() + "</h1>");

                    // Write to the library
                    using (FileStream fileStream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        using (StreamWriter sw = new StreamWriter(fileStream))
                        {
                            sw.Write(html);
                            sw.Close();
                        }
                    }

                    if (_reloadOnXmdsRefresh)
                    {
                        // Read the control meta back out
                        ReadControlMeta();

                        // Handle Navigate in here because we will not have done it during first load
                        HtmlUpdatedEvent?.Invoke(_localWebPath);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Trace.WriteLine(new LogMessage("WebMedia", "Retrived the resource, stored the document but the media has already expired."), LogType.Error.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("WebMedia", "Unknown exception " + ex.Message), LogType.Error.ToString());

                // This should exipre the media
                Duration = 5;
                base.RenderMedia(0);
            }
        }

        /// <summary>
        /// Updates the Cache File with the necessary client side injected items
        /// </summary>
        private void UpdateCacheIfNecessary()
        {
            // Ammend the resource file so that we can open it directly from the library (this is better than using a tempoary file)
            string cachedFile = "";

            using (FileStream fileStream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    cachedFile = reader.ReadToEnd();
                }
            }

            // Compare the cached dimensions in the file with the dimensions now, and 
            // regenerate if they are different.
            if (cachedFile.Contains("[[ViewPortWidth]]") || !ReadCachedViewPort(cachedFile).Equals(WidthIntended.ToString() + "x" + HeightIntended.ToString()))
            {
                // Regex out the existing replacement if present
                cachedFile = Regex.Replace(cachedFile, "<!--START_STYLE_ADJUST-->(.*)<!--END_STYLE_ADJUST-->", "");
                cachedFile = Regex.Replace(cachedFile, "<meta name=\"viewport\" content=\"width=(.*)\" />", "<meta name=\"viewport\" content=\"width=[[ViewPortWidth]]\" />");
                cachedFile = Regex.Replace(cachedFile, "<!--VIEWPORT=(.*)-->", "");
                cachedFile = Regex.Replace(cachedFile, "<!--CACHEDATE=(.*)-->", "");

                /// File should be back to its original form, ready to run through the subs again
                string html = MakeHtmlSubstitutions(cachedFile);

                // Write to the library
                using (FileStream fileStream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    using (StreamWriter sw = new StreamWriter(fileStream))
                    {
                        sw.Write(html);
                        sw.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Make Substitutions to the Cached File
        /// </summary>
        /// <param name="cachedFile"></param>
        /// <returns></returns>
        protected abstract string MakeHtmlSubstitutions(string cachedFile);

        /// <summary>
        /// Pulls the duration out of the temporary file and sets the media Duration to the same
        /// </summary>
        private string ReadCachedViewPort(string html)
        {
            // Parse out the duration using a regular expression
            try
            {
                Match match = Regex.Match(html, "<!--VIEWPORT=(.*?)-->");

                if (match.Success)
                {
                    // We have a match, so override our duration.
                    return match.Groups[1].Value;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Pulls the browser type from the provided string
        /// </summary>
        public static string ReadBrowserType(string template)
        {
            try
            {
                Match match = Regex.Match(template, "<!-- BROWSER=(.*?) -->");

                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Get the configured web media engine
        /// </summary>
        /// <param name="options"></param>
        /// <param name="isHtmlWidget">Is this for a html widget?</param>
        /// <returns></returns>
        public static WebMedia GetConfiguredWebMedia(MediaOptions options, bool isHtmlWidget)
        {
            // IE fallback for legacy players where overlapping regions are not supported
            if (ApplicationSettings.Default.FallbackToInternetExplorer)
            {
                return new WebIe(options);
            }

            // If this is a HTML widget, then always return with CEF
            if (isHtmlWidget)
            {
                return new WebCef(options);
            }

            // If we have an edge fallback, use it, otherwise see if the URL provided is in the white list.
            if (ApplicationSettings.Default.FallbackToEdge)
            {
                return new WebEdge(options);
            }
            else if (!string.IsNullOrEmpty(options.uri) && !string.IsNullOrEmpty(ApplicationSettings.Default.EdgeBrowserWhitelist)) 
            {
                // Decode the URL
                string url = Uri.UnescapeDataString(options.uri);

                // Split the white list by comma
                string[] whiteList = ApplicationSettings.Default.EdgeBrowserWhitelist.Split(',');

                foreach (string white in whiteList)
                {
                    if (url.Contains(white))
                    {
                        return new WebEdge(options);
                    }
                }
            }

            return new WebCef(options);
        }

        /// <summary>
        /// Get the configured web media engine
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static WebMedia GetConfiguredWebMedia(MediaOptions options, string type)
        {
            WebMedia media;
            if (type == "ie")
            {
                media = new WebIe(options);
            }
            else if (type == "edge")
            {
                media = new WebEdge(options);
            }
            else if (type == "cef")
            {
                media = new WebCef(options);
            }
            else
            {
                media = GetConfiguredWebMedia(options, false);
            }
            return media;
        }
    }
}
