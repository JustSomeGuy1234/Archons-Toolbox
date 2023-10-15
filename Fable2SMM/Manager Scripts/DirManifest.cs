using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows;

namespace ArchonsToolbox
{
    class DirManifest
    {
        public static string DirManifestPath { get { return ManagerInstallation.DataFolder + @"\dir.manifest"; } }
        public static string DirManifestBackupPath { get { return DirManifestPath + ".bak"; } }
        public static string DirManifestForcedPath { get { return ManagerInstallation.DataFolder + @"\dir.forced.manifest"; } }

        public const string DirManifestForcedResourcePath = ManagerInstallation.ResourcesFolder + @"\dir.forced.manifest";

        public static string CurrentDirManifestContent { get { return _currentDirManifestContent; } set { _currentDirManifestContent = value; OnCurrentDirManifestContentChanged(EventArgs.Empty); } }
        static string _currentDirManifestContent = "";
        public static event EventHandler CurrentDirManifestContentChanged;
        private static void OnCurrentDirManifestContentChanged(EventArgs e)
        { CurrentDirManifestContentChanged?.Invoke(null, e); System.Diagnostics.Trace.WriteLine("Changing dir manifest content!"); }


        public const string v10dirManifestHash = "C67DD5E9E6C6D433A5F8D88B1CECA8D0D778D71718DC5785D98D79F251E23090";
        public const string v1dirManifestHash = "69E3AD1C96B799183101349E3E538B644228276FBF1ECBAEFD02DBFBDD270A24";

        public static string AddFilesToDirManifest(List<string> files)
        {
            if (files == null)
            {
                Trace.TraceError("Error when adding mods files to dir manifest! files object is null! What else is null from the mod?");
                return CurrentDirManifestContent;
            }

            // Split manifest into strings and create a dict for efficient comparison
            List<string> newFilesLines = new List<string>();
            string[] newDirManifestLines = CurrentDirManifestContent.Split(
                                    new string[] { "\r\n", "\r", "\n" },
                                    StringSplitOptions.None
                                );
            Dictionary<string, bool> manifestAsDict = new Dictionary<string, bool>();

            foreach (string thisEntry in newDirManifestLines)
            {
                if (string.IsNullOrEmpty(thisEntry))
                    continue;
                string entryFormatted = thisEntry.Replace('/', '\\');
                if (manifestAsDict.ContainsKey(entryFormatted))
                {
                    Trace.WriteLine($"manifestAsDict already has key {entryFormatted}! The manifest probably has duplicate lines in it.");
                    continue;
                }
                manifestAsDict.Add(entryFormatted, true);
            }
            // Sanitize file string and add it to the new files list if it isn't already in the manifest dict
            foreach (string file in files)
            {
                string fileTrimmed = file.Trim().Replace('/', '\\');
                if (manifestAsDict.ContainsKey(fileTrimmed))
                    Trace.WriteLine($"File is already in dir.manifest:\n\t`{fileTrimmed}`");
                else
                    newFilesLines.Add(fileTrimmed);
            }
            newDirManifestLines = newDirManifestLines.Concat(newFilesLines).ToArray();
            return string.Join("\n", newDirManifestLines);
        }

        public static string AddModFilesToDirManifest(Mod mod)
        {
            return AddFilesToDirManifest(mod.Files);
        }

        public static void AddForcedFilesToDirManifest()
        {
            List<string> forcedfiles = File.ReadAllLines(DirManifestForcedPath).ToList();
            CurrentDirManifestContent = AddFilesToDirManifest(forcedfiles);
        }

        public static string RemoveModFilesFromDirManifest(Mod mod)
        {
            return RemoveFilesFromDirManifest(mod.Files, CurrentDirManifestContent);
        }

        public static string RemoveFilesFromDirManifest(List<string> filesToRemove, string manifestContent)
        {
            List<string> dirManifestLines = manifestContent.Split(
                                                new string[] { "\r\n", "\r", "\n" },
                                                StringSplitOptions.None
                                            ).ToList();
            filesToRemove = filesToRemove.Select(x => x.Replace('/', '\\')).ToList();
            dirManifestLines.RemoveAll(x => filesToRemove.Contains(x));
            return string.Join("\n", dirManifestLines);
        }

        public static List<string> DirManifestDeltaFromFiles(string manifestPath, string backupPath)
        {
            List<string> manFiles = File.ReadAllLines(manifestPath).ToList();
            Dictionary<string, string> backFilesDict = File.ReadAllLines(backupPath).ToDictionary<string, string>(x => x);
            List<string> delta = manFiles.Where((file, index) => !backFilesDict.ContainsKey(file)).ToList();

            return delta;
        }

        /* DirManifest Backup & Resetting */

        public static void BackupDirManifest()
        {
            if (VerifyDirManifestBackup(DirManifestBackupPath))
                return;

            if (File.Exists(DirManifestPath))
            {
                File.Copy(DirManifestPath, DirManifestBackupPath, true);
            }
            else
            {
                Trace.TraceInformation("Couldn't back up dir.manifest. Means we're on 1.0?");
            }
        }

        public static bool VerifyDirManifestBackup(string backupmanPath)
        {
            string currentBackupHash = Gamescripts.GetFileHash(backupmanPath);
            return currentBackupHash == v10dirManifestHash || currentBackupHash == v1dirManifestHash;
        }

        public static void RestoreOriginalDirManifest(bool addForcedFiles)
        {
            try
            {
                File.Copy(DirManifestBackupPath, DirManifestPath, true);
                CurrentDirManifestContent = File.ReadAllText(DirManifestPath);

                if (addForcedFiles)
                    AddForcedFilesToDirManifest();
            }
            catch (Exception ex)
            {
                string err = "Unable to restore the dir.manifest backup. You may need to re-dump if your game crashes in the main menu.";
                Trace.TraceError(err + "\n" + ex.Message);
                MessageBox.Show(err + "\nSee manager.log for more.", "Unable to restore backup", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }
        public static void ResetAndAddModsToDirManifest()
        {
            RestoreOriginalDirManifest(true);
            foreach (Mod mod in ModManaging.ModList)
            {
                CurrentDirManifestContent = AddModFilesToDirManifest(mod);
            }
            File.WriteAllText(DirManifestPath, CurrentDirManifestContent);
        }
    }
}
