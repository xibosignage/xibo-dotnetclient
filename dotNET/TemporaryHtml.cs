using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace XiboClient
{
    /// <summary>
    /// A temporary html object.
    /// Once FileContent is set it will contain the complete HTML page
    /// </summary>
    class TemporaryHtml : IDisposable
    {
        private String _fileContent;
        private String _headContent;
        private String _resourceTemplate;
        private String _filePath;

        public TemporaryHtml()
        {            
            // Load the resource file

            
        }

        /// <summary>
        /// The File content - can only be set once.
        /// </summary>
        public String BodyContent
        {
            set
            {
                // Set the contents of the file
                _fileContent = value;

                // Create the temporary file
                Store();
            }
        }

        /// <summary>
        /// The Head content - must be added before the body
        /// </summary>
        public String HeadContent
        {
            set
            {
                // Set the head content
                _headContent = value;
            }
        }

        public String Path
        {
            get
            {
                return _filePath;
            }
        }

        /// <summary>
        /// Stores the file
        /// </summary>
        private void Store()
        {
            // Create a temporary file
            _filePath = System.IO.Path.GetTempFileName();

            Debug.WriteLine(_filePath);

            // Open the resource file
            Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            
            // Use the assembly to get the HtmlTemplate resource
            using (Stream resourceStream = assembly.GetManifestResourceStream("XiboClient.Resources.HtmlTemplate.htm"))
            {
                using (TextReader tw = new StreamReader(resourceStream))
                {
                    _resourceTemplate = tw.ReadToEnd();
                }
            }

            // Insert the file content into the resource file
            _resourceTemplate = _resourceTemplate.Replace("<!--[[[HEADCONTENT]]]-->", _headContent);
            _resourceTemplate = _resourceTemplate.Replace("<!--[[[BODYCONTENT]]]-->", _fileContent);

            // Write it to the file
            using (StreamWriter sw = new StreamWriter(File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8))
            {
                sw.Write(_resourceTemplate);
                sw.Close();
            }
        }


        #region IDisposable Members

        public void Dispose()
        {
            // Remove the temporary file
            File.Delete(_filePath);
        }

        #endregion
    }
}
