using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace Fable2SMM
{
    static class ManifestParser
    {
        public static string ModManifestFilename = "modmanifest.json";

        public static JsonSerializerOptions JsonOptions { get { return _jsonOptions; } set { _jsonOptions = value; } }
        private static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { AllowTrailingCommas = true };

        /// <summary>FOR TESTING ONLY. Manifests may be created programmatically, but the Mod structure is immutable while manifests/tables are not, so new properties will be lost. A seperate tool might be created for this kind of thing.</summary>
        /// <param name="mod"></param>
        public static string CreateModManifest(Mod mod)
        {
            string modManifest = JsonSerializer.Serialize(mod, typeof(Mod), JsonOptions);
            return modManifest;
        }

        public static Mod ConvertManifestToMod(string manifestText)
        {
            if (string.IsNullOrEmpty(manifestText))
                return null;
            Mod mod = null;
            try
            {
                mod = (Mod)JsonSerializer.Deserialize(manifestText, typeof(Mod), JsonOptions);
            } catch(Exception ex)
            {
                Trace.TraceError("Error: Failed to deserialize mod manifest. Reason:\n" + ex.Message);
            }
            return mod;
        }

        public static List<string> GetAllManifestsInModFolder()
        {
            List<string> manifests = new List<string>();

            if (!Directory.Exists(ManagerInstallation.ModsFolder))
            {
                MessageBox.Show("Mods folder doesn't exist in the game's scripts directory");
                Trace.TraceError("Error: No mods folder in game installation.");
                return manifests;
            }
            foreach (string folder in Directory.EnumerateDirectories(ManagerInstallation.ModsFolder))
            {
                string finalFolder = folder + '\\';
                string manPath = finalFolder + ModManifestFilename;
                if (File.Exists(manPath))
                {
                    manifests.Add(File.ReadAllText(manPath));
                }
                else Trace.WriteLine("No manifest in folder: " + finalFolder);
            }

            return manifests;
        }

        public static string GetManifestFromModFolder(string ModID)
        {
            string manPath = ManagerInstallation.ModsFolder + ModID + "\\" + ModManifestFilename;
            if (File.Exists(manPath))
            {
                return File.ReadAllText(manPath);
            }
            else
            {
                Trace.WriteLine("Failed to find manifest for mod " + ModID);
                return null;
            }
        }

        public static List<Mod> ConvertManifestsToMods(List<string> manifests)
        {
            List<Mod> mods = new List<Mod>();

            foreach (string manifest in manifests)
            {
                Mod mod = ConvertManifestToMod(manifest);
                if (mod == null)
                    continue;
                mods.Add(mod);
            }

            return mods;
        }

        /// <summary> Parses a mod manifest and directly converts it to a Lua table.</summary>
        /// <remarks>This is best to use when installing mods because it is a direct 1:1 conversion of the properties from the manifest file, as converting the manifest to a Mod Object loses additional properties that weren't handled by the manager.</remarks>
        /// <param name="manifestText">The manifest json</param>
        /// <param name="addConfigValues">Whether or not to add `Enabled`, `Installed`, and `ClearData`.</param>
        /// <returns>The manifest in the form of a Lua table</returns>
        public static string ConvertJsonManifestIntoTable(string manifestText, bool addConfigValues = true)
        {
            if (string.IsNullOrEmpty(manifestText))
            {
                string err = "Couldn't find the manifest for a mod?";
                MessageBox.Show(err);
                Trace.TraceError("Error ConvertJsonManifestIntoTable: " + err);
                return null;
            }
            string finalTable = "{\n";
            if (addConfigValues)
            {
                finalTable += "Enabled = false, Installed = false, ClearData = false,\n";
            }

            string modKey;

            JsonDocumentOptions docOpts = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
            };
            var doc = JsonDocument.Parse(manifestText, docOpts);

            var rootElement = doc.RootElement;
            if (rootElement.ValueKind == JsonValueKind.Object)
            {
                string tableContent;
                (tableContent, modKey) = ParseJsonObjectIntoLuaTable(rootElement, 0);
                finalTable += $"{tableContent}";
            }
            else
                throw new Exception("Manifest's root element wasn't an object??? I guess make sure the modmanifest isn't empty.");

            if (string.IsNullOrWhiteSpace(modKey))
                throw new Exception("A mod manifest had no name!");

            finalTable += "}";

            return $"{modKey} = {finalTable}";
        }

        static (string tableContent, string modKey) ParseJsonObjectIntoLuaTable(JsonElement element, int depth)
        {
            string tableContent = "";
            string modKey = "error_noname";
            string unhandledValue = "";
            // Things we don't want putting into the table
            string[] skipStrings = { "authorurls", "files", "description" };

            // Todo: Clean up indentation.

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if(skipStrings.Any(x => x == property.Name.ToLower()))
                    continue;
                else if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    // Table(?)
                    tableContent += $"\n{property.Name} = {{ {ParseJsonObjectIntoLuaTable(property.Value, depth + 1)} }},\n";
                }
                else if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    // Files or AuthorURL array probably. Both of which are skipped anyway.
                    tableContent += $"\n{property.Name} = {{\n";
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        tableContent += $"\"{arrayElement.GetString()}\",\n";
                    }
                    tableContent += $"}},\n";
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                    tableContent += $"{property.Name} = \"{property.Value.GetString().Replace("\n", "\\n")}\",\n"; // Eek. GetString replaces "\n" with an actual \n.
                else if (property.Value.ValueKind == JsonValueKind.Number)
                    tableContent += $"{property.Name} = {property.Value.GetDouble()},\n";
                else
                    unhandledValue = property.Name;

                if (property.Name == "NameID")
                    modKey = property.Value.GetString();
            }
            if (!string.IsNullOrEmpty(unhandledValue))
                Trace.WriteLine($"Unhandled value type in mod {modKey} with key {unhandledValue}");
            return (tableContent, modKey);
        }

        static string Indent(int numOfIndents)
        {
            string indentation = "";
            char indentChar = '\t';
            for (int i = 0; i < numOfIndents; i++)
            {
                indentation += indentChar;
            }
            return indentation;
        }
    }
}
