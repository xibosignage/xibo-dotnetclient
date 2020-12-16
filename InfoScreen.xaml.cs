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
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using XiboClient.Log;

namespace XiboClient
{
    /// <summary>
    /// Interaction logic for InfoScreen.xaml
    /// </summary>
    public partial class InfoScreen : Window
    {
        private DispatcherTimer timer;

        public InfoScreen()
        {
            InitializeComponent();

            Loaded += InfoScreen_Loaded;
            Unloaded += InfoScreen_Unloaded;
        }

        private void InfoScreen_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unbind events
            Loaded -= InfoScreen_Loaded;
            Unloaded -= InfoScreen_Unloaded;

            // Stop the Timer
            this.timer.Tick -= Timer_Tick;
            this.timer.Stop();
        }

        private void InfoScreen_Loaded(object sender, RoutedEventArgs e)
        {
            // Update
            Update();

            // Create a timer to update the info screen
            timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Update();
        }

        /// <summary>
        /// Update from ClientInfo
        /// </summary>
        private void Update()
        {
            labelScheduleStatus.Content = ClientInfo.Instance.ScheduleStatus;
            labelRequiredFilesStatus.Content = ClientInfo.Instance.RequiredFilesStatus;
            labelXmrStatus.Content = ClientInfo.Instance.XmrSubscriberStatus;
            labelCurrentlyPlaying.Content = ClientInfo.Instance.CurrentlyPlaying;
            labelControlCount.Content = ClientInfo.Instance.ControlCount;

            textBoxSchedule.Text = ClientInfo.Instance.ScheduleManagerStatus;
            textBoxRequiredFiles.Text = ClientInfo.Instance.UnsafeList
                + Environment.NewLine 
                + ClientInfo.Instance.RequiredFilesList;

            // Log grid
            logDataGridView.Items.Clear();
            foreach (LogMessage message in ClientInfo.Instance.LogMessages.Read())
            {
                logDataGridView.Items.Add(message);
            }
        }

        private void Button_SaveLog_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.InitialDirectory = ApplicationSettings.Default.LibraryPath;
            dialog.ShowDialog();


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dialog.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document
                using (StreamWriter wrt = new StreamWriter(dialog.FileName))
                {
                    foreach (LogMessage row in logDataGridView.Items)
                    {
                        wrt.Write(row.ToString());
                        wrt.WriteLine();
                    }
                }

                MessageBox.Show("Log saved as " + dialog.FileName, "Log Saved");
            }
        }
    }
}
