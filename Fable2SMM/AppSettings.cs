using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Fable2SMM
{
    class AppSettings
    {
        public static string RootGamePath { get { return _RootGamePath; }
            set
            {
                string gamePath = Environment.ExpandEnvironmentVariables(value);
                System.Diagnostics.Trace.WriteLine("Settings root game path to " + value.ToString());
                if (Directory.Exists(gamePath))
                {
                    // Note that gamePath is the EXPANDED gamepath, basically the one we want to use. RootGamePath is what the user input.
                    _RootGamePath = value;
                    ExpandedGamePath = gamePath;
                    ModList.PopulateModList();
                }
                else
                {
                    MessageBox.Show("Path does not exist!", "Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            } 
        }
        private static string _RootGamePath = "%homepath%/Downloads/";
        public static string ExpandedGamePath { 
            get { 
                if (ExpandedGamePath != null) 
                    return _ExpandedGamePath; 
                else 
                    return ""; 
            } 
            set { _ExpandedGamePath = value; } 
        }
        private static string _ExpandedGamePath;
    }
}
