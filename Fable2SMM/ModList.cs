using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fable2SMM
{
    class ModList
    {
        public const string ScriptsPath = "/data/scripts/";
        public const string ModsFolderPath = ScriptsPath + "/Mods/";
        public const string ModManagerScriptPath = ScriptsPath + "/Mod Manager/";
        public const string InstalledModsFileName = "InstalledMods.lua";

        public static ObservableCollection<Mod> Mods{ get { return _Mods; } set { _Mods = value; } }
        private static ObservableCollection<Mod> _Mods = new ObservableCollection<Mod>();

        public static void ReadInstalledMods()
        {
            // Err i just realised I have to parse a lua file now...
            string[] wholeFileLines = File.ReadAllLines(ModManagerScriptPath + InstalledModsFileName);
            bool foundRunnerVersion = false;
            bool foundInstalledTable = false;
            bool foundInstalledTableEnd = false;

            for (int i = 0; i < wholeFileLines.Length; i++)
            {
                string line = wholeFileLines[i];

                bool hasComment = line.IndexOf("--") != -1;
                if (hasComment)
                {
                    MessageBox.Show($"InstalledMods.lua contains a comment (--) at line {i}. Please remove the comment, as the mod manager cannot handle these.", "Comments not supported", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Find runnerversion. Find installedmods.
                if (!foundRunnerVersion && line.IndexOf("runnerversion ") != -1)
                    foundRunnerVersion = true;
                if (!foundInstalledTable && line.IndexOf("installedmods ") != -1)
                {
                    foundInstalledTable = true;

                }

                if (foundInstalledTable && !foundInstalledTableEnd)
                {
                    // We should be in the installedmods table, or at the closing brace.
                    if (line.StartsWith("}"))
                        foundInstalledTableEnd = true;
                    else
                    {

                    }
                }

            }
            if (!foundRunnerVersion)
                MessageBox.Show("Failed to find Runner version. This is an indicator of a corrupt InstalledMods.lua file. You can still continue, but there may be undefined behaviour ingame.");
        }

        public static void PopulateModList()
        {
            if (Directory.Exists(AppSettings.ExpandedGamePath))
            {
                Mods.Clear();
                Trace.Write("Expanded AppSettings.ExpandedGamePath is '" + AppSettings.ExpandedGamePath + "'");

                foreach (string file in Directory.EnumerateFiles(AppSettings.ExpandedGamePath))
                {
                    Mod thisMod = new Mod
                    {
                        ModName = file
                    };
                    Mods.Add(thisMod);
                }
                Trace.WriteLine("Finished populating mod list, mods count now " + Mods.Count);
            }
            else { Trace.WriteLine("PopulateModList called, but folder doesn't exist."); }
        }
    }
}
