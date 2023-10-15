using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ArchonsToolbox
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void FilePathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab)
            {
                string path = ((TextBox)sender).Text;
                if (path != null && path != "")
                {
                    if (!System.IO.Directory.Exists(path))
                    {
                        MessageBox.Show("Directory does not exist.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    ManagerInstallation.GameFolder = path;
                    Trace.WriteLine("(through script): Set gamefolder path to " + ManagerInstallation.GameFolder);
                }
            }
        }


        private void OpenDebug_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.DebugWindow = new DebugWindow();
            MainWindow.DebugWindow.Show();
        }

        private void OpenPatching_Click(object sender, RoutedEventArgs e)
        {
            new PatchGeneration().Show();
        }

        public static RoutedCommand CheckUpdatesCmd = new RoutedCommand();
        private void CheckUpdatesCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            string latestVer = Update.GetLatestVersion();
            this.Cursor = Cursors.Arrow;

            if (!string.IsNullOrEmpty(latestVer) && latestVer.ToLower() != "releases" && Update.CurrentVersion != latestVer)
            {
                // Update is available
                if (MessageBox.Show("An update is available. Go to download page?", "Update Available", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string FinalURL = Update.UpdateURL.Split(' ')[0];
                    if (FinalURL.StartsWith("https:"))
                        Process.Start(FinalURL);
                    else
                    {
                        MessageBox.Show("Update URL is malformed. Get the download manually here:\n\n" + Update.CurrentVersion);
                        Trace.WriteLine("Final Update URL was malformed: " + FinalURL);
                    }
                }
            }
        }
    }
}
