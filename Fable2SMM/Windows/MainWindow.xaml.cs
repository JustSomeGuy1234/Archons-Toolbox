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
using System.Windows.Navigation;
using System.ComponentModel;
using System.IO;
using System.Collections;

namespace Fable2SMM
{
    public partial class MainWindow : Window
    {
        public static DebugWindow DebugWindow { get { return _debugWindow; } set { _debugWindow?.Close(); _debugWindow = value; } }
        private static DebugWindow _debugWindow;


        public MainWindow()
        {
            
            AppSettings.LoadManagerSettings();
            InitializeComponent();
#if DEBUG
            DebugWindow = new DebugWindow();
            DebugWindow.Show();
#endif
            //new animtest().Show();
            //new InteropTesting().Show();
            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (!AppSettings.StaticHasShownOOBE)
            {
                MessageBoxResult result = MessageBox.Show(
                    "It appears to be your first time using the Archon's Toolbox mod manager.\n\nPLEASE read the \"Setting Up\" and \"Deleting Mods\" sections in the help menu before continuing." +
                    "\n\nWould you like to open the help menu now? You can also open it later by clicking help at the top right.\n\n" +
                    "Clicking cancel will show this box again on startup."
                    , "Welcome!"
                    , MessageBoxButton.YesNoCancel
                );
                if (result == MessageBoxResult.Yes)
                {
                    new HelpSystem.HelpWindow().Show();
                    AppSettings.StaticHasShownOOBE = true;
                }
                else if (result == MessageBoxResult.No)
                    AppSettings.StaticHasShownOOBE = true;
                // Cancel will show again
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string fp in files)
            {
                if (File.Exists(fp))
                {
                    if (fp.EndsWith(".zip"))
                    {
                        if (ModManaging.InstallModFromZip(fp) == null)
                        {
                            MessageBox.Show("Failed to install mod from zip file. Check manager.log for more.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                            Trace.TraceError($"No {ManifestParser.ModManifestFilename} file in mod folder at {fp}!");
                        }
                    }
                    else
                        MessageBox.Show("File does not end with .zip: " + fp);
                } else if (Directory.Exists(fp))
                    ModManaging.InstallModFromFolder(fp);
                else
                {
                    MessageBox.Show("Dropped file/folder doesn't exist?", "How's this possible", MessageBoxButton.OK, MessageBoxImage.Hand);
                    return;
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        { new SettingsWindow().ShowDialog(); }
        private void InstallZip_Click(object sender, RoutedEventArgs e)
        {
            var dialogue = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Zip Archive|*.zip|All|*.*",
                Multiselect = true,
                InitialDirectory = Path.GetFullPath(@".\sample mods\")
            };
            if (dialogue.ShowDialog() == true)
            {
                foreach (string zipPath in dialogue.FileNames)
                {
                    Mod zipmod = ModManaging.InstallModFromZip(zipPath);
                    if (zipmod == null)
                    {
                        MessageBox.Show("Failed to get mod from zip. Check manager.log for more.");
                    }
                }
            }
        }
        private void InstallFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialogue = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (dialogue.ShowDialog() ?? false)
            {
                string modFolderPath = Path.GetFullPath(dialogue.SelectedPath) + "\\";
                ModManaging.InstallModFromFolder(modFolderPath);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // TODO: isDirty should be set after comparing installedmods content, and also the dir manifest?
            if (AppSettings.SettingsAreDirty || ModManaging.ModsAreDirty)
            {
                var result = ( ModManaging.AutosaveSettings ? MessageBoxResult.Yes : MessageBox.Show("Do you wish to save changes?", "Unsaved Changes", MessageBoxButton.YesNoCancel) );
                if (result == MessageBoxResult.Yes)
                    ModManaging.SaveChanges();
                else if (result == MessageBoxResult.Cancel)
                    e.Cancel = true;
            }
            if (!e.Cancel)
                App.Current.Shutdown();
            base.OnClosing(e);
        }

        // This command is invoked when a mod is double clicked or when using the context menu of a mod.
        public static RoutedCommand ManageCmd = new RoutedCommand();
        private void ManagerCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var target = e.Source;
            var arg = e.Parameter as string;
            if (target is ListView listView)
            {
                IList list = listView.SelectedItems;
                List<Mod> mods = list.OfType<Mod>().ToList();
                if (mods.Count > 1 && (arg != "invert") && MessageBox.Show($"Are you sure you want to {arg} all selected mods?", "Confirm", MessageBoxButton.YesNoCancel) != MessageBoxResult.Yes)
                    return;
                switch (arg)
                {

                    case "invert":
                        mods.ForEach(x => x.Enabled = !x.Enabled);
                        break;
                    case "enable":
                        mods.ForEach(x => x.Enabled = true);
                        break;
                    case "disable":
                        mods.ForEach(x => x.Enabled = false);
                        break;
                    case "install":
                        mods.ForEach(x => x.Installed = true);
                        break;
                    case "uninstall":
                        mods.ForEach(x => x.Installed = false);
                        break;
                    case "delete":
                        mods.ForEach(x => x.DeleteModCompletely());
                        break;
                    case "debug":
                        Mod debugMod = (Mod)listView.SelectedItem;
                        Trace.WriteLine($"----------\nNameID: {debugMod.NameID}\nDescription: {debugMod.Description}\nAuthor: {debugMod.Author}\nAuthorURLs count: {debugMod.AuthorURLs.Count}\n----------");
                        break;
                    case "open folder":
                        mods.ForEach(x => x.ViewInFolder());
                        break;
                    default:
                        MessageBox.Show("Unknown mod command: " + arg);
                        break;
                }
            }
            else
                Trace.WriteLine("Toggle Mod command target is not a ListView!");
        }
        private void ManageCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public static RoutedCommand CopyAllCmd = new RoutedCommand();
        private void CopyAllCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var arg = e.Parameter as string;
            Clipboard.SetText(arg);
        }
        private void CopyAllCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public static RoutedCommand OpenManagerInstallCmd = new RoutedCommand();
        private void OpenManagerInstallCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var installWindow = new InstallWindow();
            installWindow.Owner = this;
            installWindow.ShowDialog();
        }

        public static RoutedCommand OpenHelpCmd = new RoutedCommand();
        private void OpenHelpCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var helpWindow = new HelpSystem.HelpWindow();
            helpWindow.Owner = this;
            helpWindow.ShowDialog();
        }
    }
}