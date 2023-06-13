using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Fable2SMM
{
    class ManagerInstallation
    {

        public static event EventHandler GameFolderChanged;
        private static void OnGameFolderChanged() => GameFolderChanged?.Invoke(null, EventArgs.Empty);
        public static string GameFolder
        {
            get => _gameFolder;
            set
            {
                if (value == "/" || value == "\\" || string.IsNullOrEmpty(value))
                {
                    _gameFolder = value;
                    ModManaging.EnumerateAllMods();
                    OnGameFolderChanged(); // This needs to be called to empty the listview after being reset
                    return;
                }
                if (!value.EndsWith(@"\") && !value.EndsWith("/")) value += @"\";
                if (!Directory.Exists(value))
                {
                    MessageBox.Show("Game Path does not exist!", "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Hand);
                    return;
                }

                if (!Directory.Exists(Path.Combine(value, @"Data\")))
                {
                    MessageBox.Show("Invalid game path. The given folder does not contain /data/\n" + Path.Combine(value, @"Data\"));
                    return;
                }

                if (ModManaging.InstallIsDirty)
                {
                    MessageBoxResult result = MessageBox.Show("Would you like to save changes to the current installation before swapping?", "Save Changes?", MessageBoxButton.YesNoCancel);
                    if (result == MessageBoxResult.Yes)
                        ModManaging.SaveChanges();
                    else if (result == MessageBoxResult.Cancel)
                        return;
                }

                _gameFolder = value; OnGameFolderChanged(); Trace.WriteLine("Changing GameFolder to " + value);
                if (!AppSettings.StartingUp)
                    AppSettings.SettingsAreDirty = true;
                ModManaging.InstallIsDirty = false;

                if (File.Exists(Mod.InstalledModsPath))
                {
                    LuaParsing.ReadInstalledModsIntoContentAndDict();
                    ModManaging.EnumerateAllMods();
                }
                if (File.Exists(DirManifest.DirManifestPath))
                {
                    DirManifest.CurrentDirManifestContent = File.ReadAllText(DirManifest.DirManifestPath);
                }
                else
                {
                    MessageBox.Show("There's no dir.manifest file in your game installation. This will cause problems.");
                    Trace.WriteLine("dir.manifest not found! At: " + DirManifest.DirManifestPath);
                }

                if (!File.Exists(Gamescripts.GamescriptsPath))
                {
                    MessageBox.Show("There's no gamescripts_r.bnk file in your game installation! Are you sure you chose the right folder?");
                    // Todo: Error handling here. Reset filepath?
                }
                else
                {
                    Gamescripts.UpdateGamescriptsStatus();
                }


                AppSettings.StartingUp = false;
            }
        }
        static string _gameFolder = "";

        public static string DataFolder { get { return Path.Combine(GameFolder, @"Data\"); } }
        public static string ScriptsFolder { get { return Path.Combine(DataFolder, @"scripts\"); } }
        public static string ModsFolder { get { return Path.Combine(ScriptsFolder, @"Mods\"); } }
        public static string RunnerFolder { get { return Path.Combine(ScriptsFolder, @"Mod Manager\"); } }
        public static string RunnerGUIFolder { get { return Path.Combine(ScriptsFolder, @"GUIState\"); } }
        public static string RunnerPath => Path.Combine(RunnerFolder, "runner.lua");

        public const string ResourcesFolder = @".\resources\";
        public const string Gamescriptsv10PatchPath = ResourcesFolder + "gamescripts_r 10.1 patch.bin";
        public const string Gamescriptsv1PatchPath = ResourcesFolder + "gamescripts_r 1.0 patch.bin";
        public const string GuiscriptsPatchPath = ResourcesFolder + "guiscripts.patch";


        public static string ScriptsZipPath => Path.Combine(ResourcesFolder, "Mod Manager Scripts.zip");
        public static string GUIZipPath => Path.Combine(ResourcesFolder, "GUIManagerScripts.zip");

        public static string unchangedInstalledmodsHash = "";

        public static string GetRunnerVersion()
        {
            if (!File.Exists(RunnerPath))
                throw new Exception($"Failed to find the runner file at\n`{RunnerPath}`");
            string fileContent = File.ReadAllText(RunnerPath);
            if (fileContent.Length <= 0)
                throw new Exception("runner file is empty! (no lines)");

            Regex versionRegex = new Regex(@"Version = ([\d\.]+)", RegexOptions.Singleline);
            Match match = versionRegex.Match(fileContent);

            if (match.Success)
            {
                if (match.Groups.Count < 1)
                {
                    Trace.TraceError("Error: (Updating) Caught Version line but not version number!");
                    return "";
                }
                string ver = match.Groups[1].Value;
                Trace.WriteLine("Got installed runner version: " + ver);
                return ver;
            }
            else
            {
                Trace.TraceError("Error: Failed to get Version from Runner file!");
                return "";
            }
        }

        public static void InstallRunner()
        {
            // Not hugely proud of this mess. Should probably just have a seperate window for it.

            if (!File.Exists(ScriptsZipPath))
            {
                MessageBox.Show("ModManagerScripts.zip is missing from the manager's resource folder!\nRedownload the mod manager to fix this.", "Missing File", MessageBoxButton.OK, MessageBoxImage.Hand);
                return;
            }


            string currentGamescriptsHash = Gamescripts.GetFileHash(Gamescripts.GamescriptsPath);
            bool gamescriptsMissing = string.IsNullOrEmpty(currentGamescriptsHash);
            bool gamescriptsExists = File.Exists(Gamescripts.GamescriptsPath);
            bool gamescriptsIsOriginal = currentGamescriptsHash == Gamescripts.GamescriptsV10OriginalHash || currentGamescriptsHash == Gamescripts.GamescriptsV1OriginalHash;
            bool gamescriptsHasManager = currentGamescriptsHash == Gamescripts.GamescriptsV10ManagerHash;
            string currentBackupHash = Gamescripts.GetFileHash(Gamescripts.GamescriptsBackupPath);
            bool backupExists = File.Exists(currentBackupHash);
            bool backupIsOriginal = currentBackupHash == Gamescripts.GamescriptsV10OriginalHash || currentBackupHash == Gamescripts.GamescriptsV1OriginalHash;
            bool backupHasManager = currentBackupHash == Gamescripts.GamescriptsV10ManagerHash;

            if (!gamescriptsExists && !backupExists)
            {
                MessageBox.Show("gamescripts_r.bnk is missing from the game directory, and there is no backup.", "Missing File", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Make sure the gamescripts_r bnk is patchable, make a backup, or restore one if available.
            // TODO: Add an option to override the patching checks
            if (gamescriptsExists)
            {
                // Gamescripts is original and there's no backup
                if (gamescriptsIsOriginal && !backupExists)
                {
                    File.Copy(Gamescripts.GamescriptsPath, Gamescripts.GamescriptsBackupPath, true);
                    Gamescripts.CurrentGamescriptsStatus = GamescriptsStatus.ORIGINAL;
                }
                // Gamescripts is original, backup exists but is not original
                else if (gamescriptsIsOriginal && backupExists && !backupIsOriginal)
                {
                    if (MessageBox.Show("The gamescripts_r.bnk backup is not original. Would you like to overwrite it with the original version?", "Unoriginal backup", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        File.Copy(Gamescripts.GamescriptsPath, Gamescripts.GamescriptsBackupPath);
                    Gamescripts.CurrentGamescriptsStatus = GamescriptsStatus.ORIGINAL;
                }
                else if (!gamescriptsIsOriginal && !gamescriptsHasManager)
                {
                    if (backupExists && (backupIsOriginal || backupHasManager))
                    {
                        if (MessageBox.Show("Current gamescripts_r.bnk is not compatible, but the backup is. Would you like to restore the backup?", "Incompatible File", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            File.Copy(Gamescripts.GamescriptsBackupPath, Gamescripts.GamescriptsPath, true);
                        }
                    }
                }
            }
            // Gamescripts is not suitable or it's missing but there is a backup, original or otherwise
            else if ((!gamescriptsExists || (!gamescriptsIsOriginal && !gamescriptsHasManager)) && backupExists)
            {
                if ((backupIsOriginal || backupHasManager) && MessageBox.Show("gamescripts_r.bnk is missing or incompatible.\n\nHowever, a suitable backup exists. Would you like to restore it?", "Missing File & Corrupt Backup",
                                                                            MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    File.Copy(Gamescripts.GamescriptsBackupPath, Gamescripts.GamescriptsPath);
                }
            }

            Gamescripts.UpdateGamescriptsStatus();

            // Extract mod manager/runner scripts
            ExtractRunnerScripts();

            // By now the bnk is either original, already patched, or the user has declined to use a compatible bnk. 
            bool forcev10Patch = false;
            if (Gamescripts.CurrentGamescriptsStatus == GamescriptsStatus.MODIFIED)
            {
                MessageBoxResult stillWantsToPatch = MessageBox.Show(
                "Your gamescripts_r file is not recognised.\n\nThis may be because you're running a currently unsupported version of the game (e.g. localized), or because you've already modified the game yourself.\n\nWould you like to continue anyway?"
                , "Unknown Version", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (stillWantsToPatch == MessageBoxResult.No)
                    return;
                else
                {
                    MessageBoxResult gotyResult = MessageBox.Show(
                        "Is your version Game of the Year/Platinum Hits?\n(Shown as 10.1 or 10.1.1 in xenia's titlebar if emulating)\n\nIf you choose no, the patch for v1.0 will be applied."
                        , "Game Version", MessageBoxButton.YesNoCancel
                    );
                    if (gotyResult == MessageBoxResult.Cancel)
                        return;
                    else if (gotyResult == MessageBoxResult.Yes)
                        forcev10Patch = true;
                }
            }
            else if (Gamescripts.CurrentGamescriptsStatus == GamescriptsStatus.MANAGERINSTALLED)
            {
                MessageBox.Show("Gamescripts is already patched...");
            }
            
            // Either GoTY was detected or was chosen, or 1.0 was detected or was defaulted to.
            Patching.Patcher.Patch(Gamescripts.GamescriptsPath, forcev10Patch || Gamescripts.IsGoTY ? Gamescriptsv10PatchPath : Gamescriptsv1PatchPath);

            // Create mods folder
            Directory.CreateDirectory(ModsFolder);

            // TODO: Modify this so that the manager scripts aren't added by the forced file, and are instead added automatically.
            if (File.Exists(DirManifest.DirManifestForcedPath))
            {
                var result = MessageBox.Show("You have an old dir.forced.manifest file. Would you like to overwrite it? If you're unsure, or are updating and already have it backed up, choose yes." +
                    "\n\nIf you have customized it, copy the lines somewhere safe and reinsert them after install." +
                    "\n\nNOTICE: If you're updating you really should as the manager uses the forced file for new manager-related files.", "Overwrite forced dirmanifest?", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                    File.Copy(DirManifest.DirManifestForcedResourcePath, DirManifest.DirManifestForcedPath, true);
            }
            else
                File.Copy(DirManifest.DirManifestForcedResourcePath, DirManifest.DirManifestForcedPath, true);
            // TODO: Test this
            DirManifest.AddForcedFilesToDirManifest();
            File.WriteAllText(DirManifest.DirManifestPath, DirManifest.CurrentDirManifestContent);
            Gamescripts.UpdateGamescriptsStatus();
            if (!File.Exists(Mod.InstalledModsPath))
            {
                MessageBox.Show("Manager cannot find InstalledMods.lua after installation? This is bad! Please report this.");
                return;
            }
            ModManaging.CurrentInstalledModsContent = File.ReadAllText(Mod.InstalledModsPath);
        }
        public static void ExtractRunnerScripts()
        {
            string zipFolder = Path.GetDirectoryName(ScriptsZipPath);
            if (!Directory.Exists(zipFolder) || !File.Exists(ScriptsZipPath))
            {
                MessageBox.Show("The runner zip file is missing!\n\nThis should have been included with the manager. Redownload it to fix this.", "Missing File", MessageBoxButton.OK, MessageBoxImage.Hand);
                Trace.TraceError("Error: ScriptsZipPath (or the resources folder) is missing!");
                return;
            }
            ZipArchive modManagerScripts = new ZipArchive(File.OpenRead(ScriptsZipPath));
            foreach (ZipArchiveEntry entry in modManagerScripts.Entries)
            {

                string finalEntryPath = ScriptsFolder + entry.FullName;
                string finalEntryFolder = Path.GetDirectoryName(finalEntryPath);
                // Todo: We probably shouldn't skip this if we're updating the installedmods layout. Try to migrate old data to new?
                if (entry.Name == Mod.InstalledModsName && File.Exists(Mod.InstalledModsPath))
                    continue;

                // By the time the iterator gets to folders, we've probably already had files that rely on them existing.
                if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.EndsWith("/") || entry.Name.EndsWith("\\"))
                    continue;

                // So we just gotta create them each time.
                Directory.CreateDirectory(finalEntryFolder);

                byte[] buffer;
                Stream entryStream = entry.Open();
                entryStream.Read(buffer = new byte[entry.Length], 0, (int)entry.Length);
                File.WriteAllBytes(finalEntryPath, buffer);
            }
            MessageBox.Show("Updated!");
        }

        public static void UninstallManager()
        {
            MessageBox.Show("You will be prompted to delete individual components of the manager. Press cancel at any point to undo the process.");
            var deleteManagerConfirm = MessageBox.Show("Are you sure you want to remove all Mod Manager runner scripts?", "Confirm", MessageBoxButton.YesNoCancel);
            var deleteModsConfirm = MessageBox.Show("Are you sure you want to delete all mods? This may not delete them from your savefile.", "Confirm", MessageBoxButton.YesNoCancel);
            var unpatchConfirm = MessageBox.Show("Are you sure you want to unpatch your game?", "Confirm", MessageBoxButton.YesNoCancel);

            if ((((byte)deleteManagerConfirm) | ((byte)deleteModsConfirm) | ((byte)unpatchConfirm)) == ((byte)MessageBoxResult.Cancel))
            {
                MessageBox.Show("Canceled. No changes have been made.");
                return;
            }


            if (deleteManagerConfirm == MessageBoxResult.Yes && Directory.Exists(RunnerFolder))
            {
                Directory.Delete(RunnerFolder, true);
                string multipagePath = Path.Combine(ScriptsFolder, "Quests\\MultipageMenu.lua");
                if (File.Exists(multipagePath))
                    File.Delete(multipagePath);
                else
                    Trace.WriteLine($"Couldn't delete multipage menu. No worries. ({multipagePath})");
            }

            if (deleteModsConfirm == MessageBoxResult.Yes && Directory.Exists(ModsFolder))
                Directory.Delete(ModsFolder, true);

            if (unpatchConfirm == MessageBoxResult.Yes && File.Exists(Gamescripts.GamescriptsPath))
            {
                if (File.Exists(Gamescripts.GamescriptsBackupPath))
                    File.Copy(Gamescripts.GamescriptsBackupPath, Gamescripts.GamescriptsPath, true);
                else
                    MessageBox.Show("The backup for Gamescript_r.bnk is missing! You will need to restore the file on your own.");
            }

            Gamescripts.UpdateGamescriptsStatus();
        }

        public static void InstallGUIStuff()
        {
            bool extracted = ExtractGUIStateScripts();
            if (!extracted) return;

            PatchGUIScripts();
        }
        public static bool ExtractGUIStateScripts()
        {
            // Unzip GUI scripts into Mod Manager/GUI/
            if (!File.Exists(GUIZipPath))
            {
                MessageBox.Show("GUI scripts zip not found! Redownloading the manager should fix this.");
                return false;
            }
            var fileDict = ModManaging.GetZipFileContents(GUIZipPath);

            foreach (KeyValuePair<string, byte[]> fileKVP in fileDict)
            {
                string filePath = fileKVP.Key;
                byte[] fileData = fileKVP.Value;
                try
                    {File.WriteAllBytes(filePath, fileData);}
                catch (Exception ex)
                {
                    string err = "Failed to extract files from the GUI script zip:\n\n" + ex.Message;
                    Trace.WriteLine(err);
                    MessageBox.Show(err);
                    return false;
                }
            }
            return true;
        }
        public static void PatchGUIScripts()
        {
            // Patch guiscripts.bnk, and handle backup stuff... with hardcoded filepaths :|
            if (!File.Exists(GuiscriptsPatchPath))
            {
                string err = "Cannot find the guiscripts patch! Redownloading the manager should fix this.";
                Trace.Write(err);
                MessageBox.Show(err);
                return;
            }
            string guiscriptsPath = Path.Combine(DataFolder, "guiscripts.bnk");
            if (!File.Exists(guiscriptsPath))
            {
                string err = "Cannot find guiscripts.bnk in the current game install! Path:\n\n" + guiscriptsPath;
                Trace.Write(err);
                MessageBox.Show(err);
                return;
            }
            if (!File.Exists(guiscriptsPath + ".bak"))
            {
                File.Copy(guiscriptsPath, guiscriptsPath + ".bak");
            }
            Patching.Patcher.Patch(guiscriptsPath, GuiscriptsPatchPath);
        }
    }
}
