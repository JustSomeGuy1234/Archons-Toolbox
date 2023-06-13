using System;
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
using System.IO;

namespace Fable2SMM
{
    // This class won't contain important code so I don't care how poorly written it is
    public partial class PatchGeneration : Window
    {
        public PatchGeneration()
        {
            InitializeComponent();
        }

        private void GeneratePatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(SourcePath))
            {MessageBox.Show("SourcePath doesn't exist");return;}
            if (!File.Exists(TargetPath))
            {MessageBox.Show("TargetPath doesn't exist");return;}
            if (File.Exists(OutputPath) && MessageBox.Show("Patch already exists at OutputPath. Overwrite?", "Overwrite patch", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            Patching.Patcher.GeneratePatch(SourcePath, TargetPath, OutputPath);
        }

        public string SourcePath { 
            get => _sourcePath; 
            set => _sourcePath = value; 
        }
        private string _sourcePath = "";
        public string TargetPath { 
            get => _targetPath; 
            set => _targetPath = value; 
        }
        private string _targetPath = "";
        public string OutputPath { 
            get => _outputPath; 
            set => _outputPath = value;
        }
        private string _outputPath = "";

        
    }
}
