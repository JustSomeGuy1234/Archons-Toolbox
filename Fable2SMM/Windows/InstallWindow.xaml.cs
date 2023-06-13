using System;
using System.Collections.Generic;
using System.IO;
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
    /// Interaction logic for InstallWindow.xaml
    /// </summary>
    public partial class InstallWindow : Window
    {
        public InstallWindow()
        {
            InitializeComponent();
        }

        private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderselector = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            folderselector.Multiselect = false;
            folderselector.UseDescriptionForTitle = true;
            folderselector.Description = "Select Game Folder";
            if (folderselector.ShowDialog() ?? false)
                ManagerInstallation.GameFolder = folderselector.SelectedPath;
        }

        public static RoutedCommand InstallRunnerCmd = new RoutedCommand();
        private void InstallRunnerCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ManagerInstallation.InstallRunner();
        }

        public static RoutedCommand UninstallManagerCmd = new RoutedCommand();
        private void UninstallManagerCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (MessageBox.Show("READ ME!\n\n" +
                "Uninstalling the manager and deleting all mod files will not necessarily remove the mods from your savefile, and is not recommended.\n\n" +
                "Well behaved mods *should* remove themselves when you next load in but this is no guarantee.\n" +
                "I highly recommend uninstalling all mods through the manager then loading your save and making sure everything works. If it does, save the game and uninstall the mod manager.\n\n",
                "Proceed with Uninstallation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                ManagerInstallation.UninstallManager();
            }
        }
        private void UninstallManagerCmdCanExecute(object sender, CanExecuteRoutedEventArgs args)
        {args.CanExecute = Gamescripts.CurrentGamescriptsStatus != GamescriptsStatus.ORIGINAL;}

        public static RoutedCommand UpdateRunnerCmd = new RoutedCommand();
        private void UpdateRunnerCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            //string installedVersion = ModManaging.GetRunnerVersion();
            ManagerInstallation.ExtractRunnerScripts();

        }
        private void UpdateRunnerCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = 
                Gamescripts.CurrentGamescriptsStatus == GamescriptsStatus.ORIGINAL ||
                Gamescripts.CurrentGamescriptsStatus == GamescriptsStatus.MANAGERINSTALLED ||
                Gamescripts.CurrentGamescriptsStatus == GamescriptsStatus.MODIFIED;
        }

        public static RoutedCommand InstallGUICmd = new RoutedCommand();
        private void InstallGUICmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var result = MessageBox.Show (
                "The GUI patch is not required and should only be installed by mod authors who want access to the IO table for file reading/writing. " +
                "The patch will let you run code in the GUI state, but there is no support for GUI script mods yet so it's only for testing.\n\n" +
                "The main benefit of accessing the GUI state is to generate script patches which are to be distributed with mods, as the ingame state has no IO table.\n\nRead the help section for more. (todo: make sure to add it lol)\n" +
                "Continue?", "(OPTIONAL) Patch GUI?", MessageBoxButton.YesNo
            );
            if (result == MessageBoxResult.Yes)
                ManagerInstallation.InstallGUIStuff();
        }
    }
}
