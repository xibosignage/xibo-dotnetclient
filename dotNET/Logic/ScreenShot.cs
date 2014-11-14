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
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
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
