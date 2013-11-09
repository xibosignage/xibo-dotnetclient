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
    class TemporaryFile : IDisposable
    {
        private String _fileContent;
        private String _filePath;

        /// <summary>
        /// The File content - can only be set once.
        /// </summary>
        public String FileContent
        {
            set
            {
                // Set the contents of the file
                _fileContent = value;

                // Create the temporary file
                Store();
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

            // Write it to the file
            using (StreamWriter sw = new StreamWriter(File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                sw.Write(_fileContent);
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
