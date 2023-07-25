using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Fable2SMM
{



    public class Mod : INotifyPropertyChanged
    {
        public static string InstalledModsPath => Path.Combine(ManagerInstallation.RunnerFolder, InstalledModsName);
        public const string InstalledModsName = "installedmods.lua";

        public Mod(string NameID, string StartScriptPath, double VersionMajor, double VersionMinor, bool Installed, bool Enabled, bool ClearData, List<string> Files, string Description, string Author, List<string> AuthorURLs, bool IsFromInstalledMods)
        {

            if (string.IsNullOrEmpty(NameID) || StartScriptPath == null || Files == null)
            {
                string err = $"Tried to instantiate mod but something is null:\nname={NameID}, startscript={StartScriptPath}, files={string.Join(",", Files)}";
                MessageBox.Show(err, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                Trace.TraceError("Error: " + err);
                return;
            }

            Match invalidCharMatch = Regex.Match(NameID, "[^A-z0-9]");
            if (invalidCharMatch.Success)
            {
                Trace.TraceError("NameID contains disallowed characters!\n" +
                    NameID + "\n" +
                    $"Invalid char index: {invalidCharMatch.Index}"
                );
                MessageBox.Show("Mod NameID contains disallowed characters!\n" +
                    "They should only contain alphanumerics:\n\n" + NameID +
                    "\n\nSee manager.log for more.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Hand
                );
                return;
            }


            this._nameID = NameID;
            this._files = Files;
            this._startScriptPath = StartScriptPath;
            this._versionMajor = VersionMajor;
            this._versionMinor = VersionMinor;
            this._installed = Installed;
            this._enabled = Enabled;
            this._clearData = ClearData;
            this._isFromInstalledMods = IsFromInstalledMods;

            List<string> manifestURLs = null;
            if (IsFromInstalledMods)
            {
                // If we're in installedmods, get additional info (description, author, etc) from manifest.
                string manText = ManifestParser.GetManifestFromModFolder(NameID);
                if (manText == null)
                {
                    // Manifest does not exist anymore, indicating that the mod files have been deleted, or the entry is invalid.
                    // It should be noted that the prompt to delete the mod is not here but in any place where mods are enumerated (EnumerateAllMods as of writing).
                    // TODO: Perhaps the prompt should be put into the mod parsing loop? Probably not, as there would be no flexibility for retaining deleted mod data.
                    IsDeleted = true;
                }
                Mod manMod = ManifestParser.ConvertManifestToMod(manText);
                if (manMod != null)
                {
                    if (string.IsNullOrEmpty(Description))
                        Description = manMod.Description;
                    if (string.IsNullOrEmpty(Author))
                        Author = manMod.Author;
                    if (AuthorURLs.Count == 0)
                        manifestURLs = manMod.AuthorURLs;
                    if (Files.Count == 0)
                        _files = manMod.Files;
                }
                else
                    Trace.TraceError("Mod ctr: A mod is being created without a manifest");
            }

            if (_files.Count == 0)
            {
                string err = $"Files list for mod {NameID} is empty. There should probably be at least 1 file.";
                Trace.WriteLine(err);
                MessageBox.Show($"{err}\n\nTry redownloading and reinstalling the mod.");
            }
            this._description = Description;
            this._author = Author;
            this._authorURLs = manifestURLs ?? AuthorURLs;

            // Thumbnail is created and cached when first get'd
        }

        // Should be called by the setter
        public void SetModBool(bool value, [CallerMemberName] string property = "")
        {
            string propertyPath = $"installedmods.{this.NameID}.{property}";
            (var values, var _) = LuaParsing.ParseTable(ModManaging.CurrentInstalledModsContent, out Dictionary<string, object> newTable, new List<string> { propertyPath });
            if (!values.ContainsKey(propertyPath))
            {
                MessageBox.Show($"Couldn't modify {property ?? "nullproperty"} on mod {NameID ?? "nullmodname"}.\n\nThis error may happen if the mod is not installed by the manager properly.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                Trace.WriteLine($"Couldn't toggle {property ?? "nullproperty"} on mod {NameID ?? "nullmodname"}");
                // TODO: Add option to reset installedmods
                return;
            }

            ModManaging.InstalledModsFileDict = newTable;
            ModManaging.CurrentInstalledModsContent = LuaParsing.ReplaceNextWordInString(ModManaging.CurrentInstalledModsContent, value.ToString().ToLower(), values[propertyPath]);

            //Setting CurrentInstalledModsContent sets ModsAreDirty
            //ModManaging.ModsAreDirty = true;
        }

        public static void InstallMod(Mod mod)
        {
            throw new NotImplementedException("Use the InstallModIntoInstalledModsAndAddToList method instead. Installing a mod requires a ModManifest.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="manifestContent">The mods manifest used to create the table string for installedmods. If null, tries to get the folder name from the NameID.</param>
        public void InstallModIntoInstalledModsAndAddToList(string manifestContent = null)
        {
            if (string.IsNullOrEmpty(manifestContent))
                manifestContent = ManifestParser.GetManifestFromModFolder(NameID);
            if (string.IsNullOrEmpty(manifestContent))
            {
                Trace.TraceError("InstallModIntoInstalledModsAndAddToList: GetManifestFromModFolder returned null string. This mod really has no manifest?");
                MessageBox.Show("Failed to install mod. Check manager.log for more.");
                return;
            }

            if (LuaParsing.IsModInInstalledMods(NameID))
            {
                UpdateMod();
                return;
            }
            string modTableString = ManifestParser.ConvertJsonManifestIntoTable(manifestContent);

            string newContent = LuaParsing.AddTableToInstalledMods(ModManaging.CurrentInstalledModsContent, modTableString, NameID);
            if (string.IsNullOrEmpty(newContent))
                return;

            ModManaging.CurrentInstalledModsContent = newContent;
            Installed = true;
            IsFromInstalledMods = true;
            DirManifest.CurrentDirManifestContent = DirManifest.AddModFilesToDirManifest(this);
            ModManaging.ModList.Add(this);
            ModManaging.ModsAreDirty = true;
        }

        /// <summary>
        /// Takes info from the Mod's manifest in the Mods folder then updates the dir.manifest & installedmods.lua files appropriately.
        /// </summary>
        public void UpdateMod()
        {
            // Basically take the new version's manifest (and convert it to a table), then add the old management values to it, then replace the old version in installedmods with the new version.
            bool wasInstalled, wasEnabled, wasClearData;
            wasInstalled = Installed;
            wasEnabled = Enabled;
            wasClearData = ClearData;
            string wasInstalledString = wasInstalled.ToString().ToLower();
            string wasEnabledString = wasEnabled.ToString().ToLower();
            string wasClearDataString = wasClearData.ToString().ToLower();

            string newManifest = ManifestParser.GetManifestFromModFolder(NameID);
            string newModTableString = ManifestParser.ConvertJsonManifestIntoTable(newManifest, true);
            // Keep the mod's installed bool the same:
            (var foundBoolOffsets, var foundKeyOffsets) = LuaParsing.ParseTable(newModTableString, out Dictionary<string, object> newModTableDict, new List<string>() { $"{NameID}.Installed" });
            if (foundBoolOffsets.Count <= 0)
                throw new Exception("Failed to find Installed bool in mod " + NameID + " (from manifest)");
            newModTableString = LuaParsing.ReplaceNextWordInString(newModTableString, wasInstalledString, foundBoolOffsets[$"{NameID}.Installed"]);
            // Keep the mod's enabled bool the same:
            (foundBoolOffsets, foundKeyOffsets) = LuaParsing.ParseTable(newModTableString, out newModTableDict, new List<string>() { $"{NameID}.Enabled" });
            if (foundBoolOffsets.Count <= 0)
                throw new Exception("Failed to find Enabled bool in mod " + NameID + " (from manifest)");
            newModTableString = LuaParsing.ReplaceNextWordInString(newModTableString, wasEnabledString, foundBoolOffsets[$"{NameID}.Enabled"]);
            // Keep the mod's cleardata bools the same:
            (foundBoolOffsets, foundKeyOffsets) = LuaParsing.ParseTable(newModTableString, out newModTableDict, new List<string>() { $"{NameID}.ClearData" });
            if (foundBoolOffsets.Count <= 0)
                throw new Exception("Failed to find ClearData bool in mod " + NameID + " (from manifest)");
            newModTableString = LuaParsing.ReplaceNextWordInString(newModTableString, wasClearDataString, foundBoolOffsets[$"{NameID}.ClearData"]);

            // Now that we have the new mod version, we need to replace the old one by removing it and inserting the new one
            string modPath = $"installedmods.{NameID}";
            (foundBoolOffsets, foundKeyOffsets) = LuaParsing.ParseTable(ModManaging.CurrentInstalledModsContent, out var installedModsDict, new List<string>() { modPath });
            if (foundKeyOffsets.Count <= 0)
                throw new Exception("Failed to find Mod " + NameID + " in installedmods to remove for updating!");

            // TODO: This is stupid. We should simply replace (this) Mod in the ModList with the newMod.
            // Update mod info
            {
                // Files
                Mod newMod = ManifestParser.ConvertManifestToMod(newManifest);
                var newModfiles = newMod.Files;
                var redundantFiles = new List<string>();
                redundantFiles.AddRange(Files.Where(x => !newModfiles.Contains(x)));
                Trace.WriteLine($"Removing {redundantFiles.Count} files of {NameID} from dir manifest");


                DirManifest.CurrentDirManifestContent = DirManifest.RemoveFilesFromDirManifest(redundantFiles, DirManifest.CurrentDirManifestContent);
                DirManifest.CurrentDirManifestContent = DirManifest.AddModFilesToDirManifest(newMod);
                Files = newModfiles;

                // Obtain new info (description, author etc.)
                Author = newMod.Author;
                Description = newMod.Description;
                AuthorURLs = newMod.AuthorURLs;
            }

            

            // Remove old mod table and insert the new one
            ModManaging.CurrentInstalledModsContent = LuaParsing.RemoveNextTableInString(ModManaging.CurrentInstalledModsContent, foundKeyOffsets[modPath]);
            ModManaging.CurrentInstalledModsContent = LuaParsing.AddTableToInstalledMods(ModManaging.CurrentInstalledModsContent, newModTableString, NameID);

            LuaParsing.ParseTable(ModManaging.CurrentInstalledModsContent, out installedModsDict); // Reassign the new installedmods dictionary containing the new mod version to the public property
            ModManaging.InstalledModsFileDict = installedModsDict;

            ModManaging.ModsAreDirty = true;
        }


        public void DeleteModCompletely()
        {
            MessageBoxResult result = MessageBox.Show("Delete " + NameID + " and its files?\n\nThis may not remove the mod entirely on its own. Read the help section for more.", "Delete Mod", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes)
                return;

            // First we remove the mod's table from installedmods
            string search = $"installedmods.{NameID}";
            RemoveModFromInstalledMods(search);

            // Remove the mods dir.manifest entries
            DirManifest.CurrentDirManifestContent = DirManifest.RemoveFilesFromDirManifest(Files, DirManifest.CurrentDirManifestContent);
            ModManaging.InstalledModsFileDict.Remove(search);

            this.Thumbnail = null;
            ModManaging.ModList.Remove(this);

            string finalFolderPath = Path.Combine(ManagerInstallation.ModsFolder, NameID);
            if (finalFolderPath == ManagerInstallation.ModsFolder)
            {
                Trace.WriteLine("Tried to delete entire mods folder! ID of this mod must be invalid: " + NameID ?? "nullname");
                MessageBox.Show("The attempt to delete the mod's files has failed. You can delete them yourself at /Fable 2/Data/Scripts/Mods/*ModID*");
                return;
            }


            if (!Directory.Exists(finalFolderPath))
                MessageBox.Show($"Failed to find folder {NameID} in /Mods/ folder. This is fine if you've manually deleted the mod files.");
            if (Directory.Exists(finalFolderPath))
                Directory.Delete(finalFolderPath, true);
        }

        public void RemoveModFromInstalledMods(string ModPathInManifest)
        {
            (var _, var keyPos) = LuaParsing.ParseTable(ModManaging.CurrentInstalledModsContent, out var _, new List<string> { ModPathInManifest });
            if (!keyPos.ContainsKey(ModPathInManifest))
            {
                Trace.WriteLine("Failed to find mod in installedmods.lua to remove: " + NameID ?? "nullname" + " This would happen when the mod is not installed but the files exist.");
            }
            else
                ModManaging.CurrentInstalledModsContent = LuaParsing.RemoveNextTableInString(ModManaging.CurrentInstalledModsContent, keyPos[ModPathInManifest]);
        }

        public void ViewInFolder()
        {
            if (Directory.Exists(ModFolder))
                Process.Start(ModFolder); // Sanitization has occured in the constructor and from the Directory.Exists call.
            else
            {
                Trace.Write($"Failed to open folder for {NameID}." +
                    $"\n\tInstallation: {Installed}" +
                    $"\n\tIsFromInstalledMods: {IsFromInstalledMods}");
                MessageBox.Show($"Failed to open folder for {NameID}. Is it installed? Is the NameID mismatched with the folder?");
            }
        }




        public override string ToString() => NameID;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        const string DefaultDescription = "Fallback Description.\n\nYou'll see this if a mod has no Description, or the mod has been deleted improperly.";
        const string DefaultAuthor = "Fallback Author Name";

        public string ModFolder => Path.Combine(ManagerInstallation.ModsFolder, NameID) + '\\';

        public string NameID { get { return _nameID ?? "NULLMODNAME"; } set { _nameID = value; OnPropertyChanged(); } }
        private string _nameID;
        public string StartScriptPath { get { return _startScriptPath; } set { _startScriptPath = value; OnPropertyChanged(); } }
        private string _startScriptPath;
        public double VersionMajor { get { return _versionMajor; } set { _versionMajor = value; OnPropertyChanged(); } }
        private double _versionMajor;
        public double VersionMinor { get { return _versionMinor; } set { _versionMinor = value; OnPropertyChanged(); } }
        private double _versionMinor;

        
        
        public bool Installed
        {
            get { return _installed; }
            set
            {
                bool isInInstalledMods = LuaParsing.IsModInInstalledMods(NameID);
                if (!isInInstalledMods && !value)
                {
                    Trace.WriteLine("Tried to uninstall a mod that isn't in the manifest.");
                    return;
                }
                if (value)
                {
                    if (!isInInstalledMods)
                    {
                        if (!IsFromInstalledMods) // TODO: There is almost certainly a better way. Maybe we should store the manifest text in the Mod object? This is kinda gross
                        {
                            string manifestString = ManifestParser.GetManifestFromModFolder(NameID);
                            if (string.IsNullOrEmpty(manifestString))
                            {
                                string err = $"Failed to find the modmanifest for `{NameID}`. Either its NameID does not match its folder, or the mod has no manifest.";
                                Trace.TraceError(err);
                                MessageBox.Show(err, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                                return;
                            }
                            string manifestAsTable = ManifestParser.ConvertJsonManifestIntoTable(manifestString);
                            if (manifestAsTable == null)
                                return;
                            ModManaging.CurrentInstalledModsContent = LuaParsing.AddTableToInstalledMods(ModManaging.CurrentInstalledModsContent, manifestAsTable, NameID);
                        }
                        else
                            throw new Exception("Mod is not in installedmods file but is also not from manifest?\nThis would only happen if we retained a manifest from a zip/folder, OR if the manifest was deleted at runtime.");
                    }
                }
                _installed = value;
                SetModBool(value);
                if (value)
                {
                    DirManifest.CurrentDirManifestContent = DirManifest.AddModFilesToDirManifest(this);
                }
                if (!value)
                {
                    Enabled = false;
                    DirManifest.CurrentDirManifestContent = DirManifest.RemoveModFilesFromDirManifest(this);
                }
                OnPropertyChanged();
            }
        }
        private bool _installed;

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                bool isInInstalledMods = LuaParsing.IsModInInstalledMods(NameID);
                if (value && !Installed)
                    Installed = true;
                _enabled = value;
                if (isInInstalledMods)
                    SetModBool(value);
                OnPropertyChanged();
            }
        }
        private bool _enabled;
        public bool ClearData
        {
            get { return _clearData; }
            set
            {
                bool isInInstalledMods = LuaParsing.IsModInInstalledMods(NameID);
                if (!isInInstalledMods)
                    return;
                _clearData = value;
                SetModBool(value);
                OnPropertyChanged();
            }
        }
        private bool _clearData;

        public List<string> Files { get { return _files; } set { _files = value; } }
        private List<string> _files;

        public string Description { get { return _description ?? DefaultDescription; } set { _description = value ?? DefaultDescription; } }
        private string _description;
        public string Author { get { return _author ?? DefaultAuthor; } set { _author = value ?? DefaultAuthor; } }
        private string _author;
        public List<string> AuthorURLs { get { return _authorURLs ?? new List<string>(); } set { _authorURLs = value; OnPropertyChanged(); } }
        private List<string> _authorURLs;

        public bool IsOutOfDate { get { return _isOutOfDate; } set { _isOutOfDate = value; } }
        private bool _isOutOfDate;
        public bool IsDeleted { get { return _isDeleted; } set { _isDeleted = value; } }
        private bool _isDeleted;

        public bool IsFromInstalledMods { get { return _isFromInstalledMods; } set { _isFromInstalledMods = value; } }
        private bool _isFromInstalledMods;

        public bool HasImage { get { return ThumbnailUri != null; } }
        public BitmapImage Thumbnail { 
            get {
                // Returning null causes the null binding fallback value to be used, which is the placeholder lua image.

                // Thumbnail needs to be cached so we can delete it at runtime
                if (_thumbnail == null)
                {
                    if (File.Exists(ThumbnailUri.LocalPath))
                    {
                        this._thumbnail = new BitmapImage();
                        this._thumbnail.BeginInit();
                        this._thumbnail.UriSource = ThumbnailUri;
                        this._thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                        this._thumbnail.EndInit();
                    }
                }
                return _thumbnail;
            } 
            set {
                // Probably should never be called
                _thumbnail = value;
                OnPropertyChanged();
            }
        }
        private BitmapImage _thumbnail;

        private Uri ThumbnailUri {
            get => new Uri( Path.Combine(ManagerInstallation.ModsFolder, NameID, "thumb.png") );
        }
    }
}
