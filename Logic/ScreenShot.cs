using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace XiboClient.Logic
{
    class ScreenShot
    {
        public static void TakeAndSend()
        {
            // Immediately clear the request for a screenshot
            ApplicationSettings.Default.ScreenShotRequested = false;

            Rectangle bounds;

            // Override the default size if necessary
            if (ApplicationSettings.Default.SizeX != 0 || ApplicationSettings.Default.SizeY != 0)
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

                // Resize?
                if (ApplicationSettings.Default.ScreenShotSize != 0)
                {
                    Size thumbSize;
                    double ratio = (double)bounds.Width / (double)bounds.Height;

                    if (bounds.Width > bounds.Height)
                    {
                        // Landscape
                        thumbSize = new Size(ApplicationSettings.Default.ScreenShotSize, (int)(ApplicationSettings.Default.ScreenShotSize / ratio));
                    }
                    else
                    {
                        // Portrait
                        thumbSize = new Size((int)(ApplicationSettings.Default.ScreenShotSize * ratio), ApplicationSettings.Default.ScreenShotSize);
                    }

                    // Create a bitmap at our desired resolution
                    using (Bitmap thumb = new Bitmap(bitmap, thumbSize.Width, thumbSize.Height))
                    {
                        send(thumb);
                    }
                }
                else
                {
                    send(bitmap);
                }
            }
        }

        private static void send(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Jpeg);

                byte[] bytes = stream.ToArray();

                using (xmds.xmds screenShotXmds = new xmds.xmds())
                {
                    screenShotXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=submitScreenshot";
                    screenShotXmds.SubmitScreenShotCompleted += screenShotXmds_SubmitScreenShotCompleted;
                    screenShotXmds.SubmitScreenShotAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, bytes);
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
