using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace XiboClient.Logic
{
    class ScreenShot
    {
        public static void TakeAndSend()
        {
            Rectangle bounds;

            // Override the default size if necessary
            if (ApplicationSettings.Default.SizeX != 0)
            {
                bounds = new Rectangle((int)ApplicationSettings.Default.OffsetX, (int)ApplicationSettings.Default.OffsetY, (int)ApplicationSettings.Default.SizeX, (int)ApplicationSettings.Default.SizeY);
            }
            else
            {
                bounds = new Rectangle(0, 0, SystemInformation.PrimaryMonitorSize.Width, SystemInformation.PrimaryMonitorSize.Height);
            }

            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    Point p = new Point(bounds.X, bounds.Y);
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Jpeg);

                    byte[] bytes = stream.ToArray();
                    
                    using (xmds.xmds screenShotXmds = new xmds.xmds())
                    {
                        screenShotXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds;
                        screenShotXmds.SubmitScreenShotCompleted += screenShotXmds_SubmitScreenShotCompleted;
                        screenShotXmds.SubmitScreenShotAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, bytes);
                    }
                }
            }
        }

        static void screenShotXmds_SubmitScreenShotCompleted(object sender, xmds.SubmitScreenShotCompletedEventArgs e)
        {
            if (e.Error != null)
                Trace.WriteLine(new LogMessage("ScreenShot - Take", e.Error.Message), LogType.Error.ToString());
        }
    }
}
