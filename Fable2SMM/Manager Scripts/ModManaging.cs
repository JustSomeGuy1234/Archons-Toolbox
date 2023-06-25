using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Fable2SMM
{

    public static class ModManaging
    {
        public static bool AutosaveSettings = true;
        public static bool ModsAreDirty 
            { 
            get => _modsAreDirty;
            set {  
                if (value && ModManaging.AutosaveSettings)
                {
                    ModManaging.SaveChanges();
                    _modsAreDirty = false;
                }
            }
        }
        private static bool _modsAreDirty = false;
        


        public static ObservableCollection<Mod> ModList { get { return _modList; } set { _modList = value; ModListChanged?.Invoke(null, EventArgs.Empty); Trace.WriteLine("Setting ModList to something else"); } }
        private static ObservableCollection<Mod> _modList = new ObservableCollection<Mod>();
        public static event EventHandler ModListChanged;

        public static string CurrentInstalledModsContent { 
            get { return _currentInstalledModsContent; } 
            set {
                if (string.IsNullOrEmpty(value))
                {
                    Trace.TraceError("Tried to make CurrentInstalledModsContent empty!");
                    return;
                }
                _currentInstalledModsContent = value; 
                OnInstalledModsContentChanged(EventArgs.Empty);
                ModManaging.ModsAreDirty = true;
            }
        }
        static string _currentInstalledModsContent = "";
        public static event EventHandler CurrentInstalledModsContentChanged;
        private static void OnInstalledModsContentChanged(EventArgs e)
        {CurrentInstalledModsContentChanged?.Invoke(null, e);}

        public static Dictionary<string, object> InstalledModsFileDict { get { return _installedModsDict; } set { _installedModsDict = value; } }
        static Dictionary<string, object> _installedModsDict = new Dictionary<string, object>();


        public static void SaveChanges()
        {
            Trace.WriteLine("Saving...");
            // TODO: Error handling
            if (File.Exists(Mod.InstalledModsPath))
                File.WriteAllText(Mod.InstalledModsPath, ModManaging.CurrentInstalledModsContent);
            if (File.Exists(DirManifest.DirManifestPath) && DirManifest.CurrentDirManifestContent.Length > 0)
                File.WriteAllText(DirManifest.DirManifestPath, DirManifest.CurrentDirManifestContent);
            string settingsContent = JsonSerializer.Serialize(AppSettings.Inst, ManifestParser.JsonOptions);
            File.WriteAllText(AppSettings.SettingsPath, settingsContent);
        }

        /// <summary>Compares installedmods and loose mods in the Mods folder to see what needs updating/removing.</summary>
        /// <remarks>Perhaps this method shouldn't exist, and instead mods should only be marked as different when something changes (files are missing, or an intentional act through the manager)</remarks>
        /// <param name="installedmodsDict"></param>
        /// <returns> <list type="bullet">
        ///     <item>outOfDateMods - Mods with a differing version in the folder</item>
        ///     <item>neverInstalledMods - Mods that are found in folder but not installedmods</item>
        ///     <item>uninstalledMods - Mods that are found within installedmods but not folder</item>
        /// </list> </returns>
        public static void EnumerateAllMods()
        {
            if (string.IsNullOrEmpty(CurrentInstalledModsContent))
                //throw new Exception("CurrentInstalledModsContent variable is empty!");
                return;
            if (InstalledModsFileDict == null)
                throw new Exception("InstalledModsFileDict is null, but CurrentInstalledModsContent is not!");

            List<Mod> installedmodsList = LuaParsing.GetAllModsFromInstalledModsFileDict(InstalledModsFileDict);
            List<Mod> loosemodsList = ManifestParser.ConvertManifestsToMods(ManifestParser.GetAllManifestsInModFolder());
            List<Mod> FinalModList = new List<Mod>();

            Dictionary<string, Mod> installedModsDict = new Dictionary<string, Mod>();

            foreach (Mod installedmod in installedmodsList)
            {
                if (installedModsDict.ContainsKey(installedmod.NameID))
                    throw new Exception("Somehow a mod with the same nameID was found twice! " + installedmod.NameID);
                installedModsDict.Add(installedmod.NameID, installedmod);

                if (installedmod.IsDeleted)
                {
                    var result = MessageBox.Show(
                            $"{installedmod.NameID}'s manifest file is missing. You have probably tried deleting it without using the manager.\n\nWould you like to remove it from the manager?",
                            "Mod Files Missing", MessageBoxButton.YesNo, MessageBoxImage.Warning
                    );
                    if (result == MessageBoxResult.Yes)
                    {
                        installedmod.DeleteModCompletely();
                        continue;
                    }
                }

                installedmod.IsDeleted = !Directory.Exists(ManagerInstallation.ModsFolder + installedmod.NameID);
                if (installedmod.IsDeleted)
                {
                    // TODO: This will show two prompts.
                    // TODO: Add an ignore option to prevent any further prompts in the config. Or something.
                    if (MessageBox.Show("The folder for " + installedmod.NameID + " is missing. Delete the mod?", "Missing Files", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        installedmod.DeleteModCompletely();
                        continue;
                    }
                }
                FinalModList.Add(installedmod);
            }

            foreach (Mod looseMod in loosemodsList)
            {
                if (installedModsDict.ContainsKey(looseMod.NameID))
                {
                    Mod installedmod = installedModsDict[looseMod.NameID];

                    // Found existing mod
                    if (installedmod.VersionMajor != looseMod.VersionMajor || installedmod.VersionMinor != looseMod.VersionMinor)
                    {
                        installedmod.IsOutOfDate = true;
                        Trace.WriteLine("Versions differ for mod: " + installedmod.NameID);
                    }

                }
                else
                    FinalModList.Add(looseMod);

            }

            ModList = new ObservableCollection<Mod>(FinalModList);
            // We now have every mod processed from both lists, let's concat them while making sure there's no duplicates and that they retain their management status'

        }

        static void UpdateAllOutOfDateMods()
        {
            EnumerateAllMods();

            foreach (Mod oldMod in ModList)
            {
                Trace.WriteLine("Updating " + oldMod.NameID);
                if (oldMod.IsOutOfDate)
                    oldMod.UpdateMod();
            }
        }

        public static void WriteToInstalledMods(string content)
        {
            if (!Directory.Exists(Mod.InstalledModsPath.Substring(0, Mod.InstalledModsPath.LastIndexOf('\\'))))
                throw new Exception("Mod Manager folder doesn't exist. TODO: Handle this properly");
            File.WriteAllText(Mod.InstalledModsPath, content);
        }

        
        public static bool ModHasFolder(Mod mod)
        {
            return Directory.Exists(Path.Combine(ManagerInstallation.ModsFolder, mod.NameID));
        }
        public static void WriteFilesToModFolders(Dictionary<string, byte[]> files)
        {
            foreach (string relativeFilePath in files.Keys)
            {
                string folderPath = Path.Combine(ManagerInstallation.ModsFolder, Path.GetDirectoryName(relativeFilePath));
                string fullFilePath = Path.Combine(ManagerInstallation.ModsFolder, relativeFilePath);
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                File.WriteAllBytes(fullFilePath, files[relativeFilePath]);
            }
        }

        public static Dictionary<string, byte[]> GetZipFileContents(string zipPath)
        {
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

            try
            {
                using (Stream fs = File.OpenRead(zipPath))
                {
                    ZipArchive archive = new ZipArchive(fs);
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        byte[] filecontent = new byte[entry.Length];
                        using (Stream entryStream = entry.Open())
                        {
                            entryStream.Read(filecontent, 0, filecontent.Length);
                        }
                        files.Add(entry.FullName, filecontent);
                    }
                }
            } catch (Exception e)
            {
                Trace.WriteLine(e);
            }
            return files;
        }
        public static (Mod, string manifestContent, Dictionary<string, byte[]>) GetModFromZip(string zipPath)
        {
            Mod modFromManifest = null;
            string manifestContent = "";
            var files = GetZipFileContents(zipPath);
            if (files == null)
            {
                Trace.TraceError("GetModFromZip Error: GetZipFileContents returned null");
                return (null, null, null);
            }
            foreach (string filePath in files.Keys)
            { 
                if (filePath.Split(new char[] { '/', '\\' }).Contains("modmanifest.json"))
                {
                    string contentString = ASCIIEncoding.ASCII.GetString(files[filePath]);
                    modFromManifest = ManifestParser.ConvertManifestToMod(contentString);
                    if (modFromManifest != null)
                        manifestContent = contentString;
                }
            }
            if (modFromManifest == null)
            {
                Trace.TraceError("Error: GetModFromZip Failed to find mod manifest from zip file " + zipPath);
                return (null,null,null);
            }
            return (modFromManifest, manifestContent, files);
        }
        public static Mod InstallModFromZip(string zipPath)
        {
            (Mod zipMod, string manifestContent, var zipFilesDict) = GetModFromZip(zipPath);
            if (zipMod == null)
            {
                Trace.TraceError("Error: InstallModFromZip received no mod from GetModFromZip");
                MessageBox.Show("Error: Couldn't get mod from zip", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                return null;
            }
            if (ModManaging.ModHasFolder(zipMod))
            {
                if (MessageBox.Show($"A mod with this ID ({zipMod.NameID}) is already installed. Update?", "Update Mod", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    return null;
            }
            WriteFilesToModFolders(zipFilesDict);
            if (LuaParsing.IsModInInstalledMods(zipMod.NameID))
            {
                Mod existingMod = ModList.First(x => x.NameID == zipMod.NameID);
                if (existingMod == null)
                    throw new NullReferenceException("Mod is already in InstalledMods but can't be found in the ModList to update???\nI think I did something stupid here so this error may be made in error.");
                existingMod.UpdateMod();
                return existingMod;
            }
            zipMod.InstallModIntoInstalledModsAndAddToList(manifestContent);
            return zipMod;
        }
        public static (Mod, string, Dictionary<string, byte[]>) GetModFromFolder(string passedPath)
        {
            string folderPath = passedPath;
            if (!folderPath.EndsWith("/") && !folderPath.EndsWith("\\"))
                folderPath += "\\";
            string modManPath = Path.Combine(folderPath + "modmanifest.json");
            if (!File.Exists(modManPath))
            {
                MessageBox.Show("Folder doesn't contain a mod manifest: " + folderPath);
                return (null, null, null);
            }

            string modManContent = File.ReadAllText(modManPath);
            Mod mod = ManifestParser.ConvertManifestToMod(modManContent);
            Dictionary<string, byte[]> fileDict = new Dictionary<string, byte[]>();


            string[] fullPaths = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            foreach (string fullpath in fullPaths)
            {
                byte[] content = File.ReadAllBytes(fullpath);
                string relPath = Path.Combine(mod.NameID, fullpath.Replace(folderPath.Replace('/', '\\'), ""));
                fileDict.Add(relPath, content);
            }



            return (mod, modManContent, fileDict);
        }
        public static Mod InstallModFromFolder(string folderPath)
        {
            (Mod folderMod, string modManContent, Dictionary<string, byte[]> fileDict) = GetModFromFolder(folderPath);
            if (folderMod == null)
                return null;

            if (ModManaging.ModHasFolder(folderMod))
            {
                if (MessageBox.Show($"A mod with this ID ({folderMod.NameID}) is already installed. Update?", "Update Mod", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    return null;
            }
            WriteFilesToModFolders(fileDict);
            folderMod.InstallModIntoInstalledModsAndAddToList(modManContent);
            Mod existingMod = ModList.First(x => x.NameID == folderMod.NameID);


            if (LuaParsing.IsModInInstalledMods(folderMod.NameID))
            {
                if (existingMod == null)
                    throw new NullReferenceException("Mod is already in InstalledMods but can't be found in the ModList to update???\nI think I did something stupid here so this error may be made in error.");
            }
            existingMod.UpdateMod();
            return existingMod;

        }
    }
}
