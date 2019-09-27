using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using System.IO.Compression;

namespace XiboClient
{
    static class WebMedia
    {
        /// <summary>
        /// Get WebMedia object
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Media GetWebMedia(RegionOptions options)
        {
            if (UsingChrome())
                return new CefWebMedia(options);
            else
                return new IeWebMedia(options);
        }

        public static Media GetHtmlPackage(RegionOptions options)
        {
            string pathToMediaFile = Path.Combine(ApplicationSettings.Default.LibraryPath, options.uri);
            string pathToPackageFolder = Path.Combine(ApplicationSettings.Default.LibraryPath, "package_" + options.FileId);
            string pathToStatusFile = Path.Combine(pathToPackageFolder, "_updated");

            // Configure the file path to indicate which file should be opened by the browser
            var _filePath = ApplicationSettings.Default.EmbeddedServerAddress + "package_" + options.FileId + "/" + options.Dictionary.Get("nominatedFile", "index.html");

            // Check to see if our package has been extracted already
            // if not, then extract it
            if (!(Directory.Exists(pathToPackageFolder) && IsUpdated(pathToStatusFile, File.GetLastWriteTime(pathToMediaFile))))
            {
                // Extract our file into the specified folder.
                ZipFile.ExtractToDirectory(pathToMediaFile, pathToPackageFolder);

                // Add in our extraction date.
                WriteUpdatedFlag(pathToStatusFile);
            }

            //Set URI to file path
            options.uri = _filePath;
            //Set modeid =  1
            options.Dictionary.Replace("modeid", "1");

            return GetWebMedia(options);
        }

        /// <summary>
        /// Check whether to use chrome
        /// </summary>
        /// <returns></returns>
        public static bool UsingChrome()
        {
            return ApplicationSettings.Default.BrowserType.Equals("chrome", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Updated Flag
        /// </summary>
        /// <param name="path"></param>
        private static void WriteUpdatedFlag(string path)
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
        private static bool IsUpdated(string path, DateTime lastModified)
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
