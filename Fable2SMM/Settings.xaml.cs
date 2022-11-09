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

namespace Fable2SMM
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // Fuck mvvm.
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
                    AppSettings.RootGamePath = path;
                    Trace.WriteLine("Set appsettings install path to " + AppSettings.RootGamePath);
                    ModList.PopulateModList();
                }
            }
        }
    }
}
