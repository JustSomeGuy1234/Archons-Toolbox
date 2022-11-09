using System;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Security.Cryptography;

namespace Fable2SMM
{

    class LuaParsing
    {

        public static Dictionary<string, object> CastObjectToTable(object obj)
        { return (Dictionary<string, object>)obj; }

        public static bool TryCastObjectToTable(object obj, out Dictionary<string, object> dict)
        {
            dict = new Dictionary<string, object>();
            try
            {
                dict = (CastObjectToTable(obj));
                return true;
            }
            catch { return false; }
        }

        public static bool IsNextCharacterAValue(string toParse)
        {
            Regex nextCharRegex = new Regex(@"[""'\d\w{]", RegexOptions.Multiline);
            Match match = nextCharRegex.Match(toParse);

            if (match.Success)
            {
                char nextCharacter = toParse[match.Index];
                if (char.IsLetter(nextCharacter))
                    return false;
                else
                    return true;
            }
            else return false;
        }

        public static (string nextWord, int offset) GetNextWordInString(string toParse)
        {
            string nextWord = null;
            int offset = -1;
            Regex FirstWordRegex = new Regex(@"\s*([A-z0-9_]+)");
            Match WordMatch = FirstWordRegex.Match(toParse);
            if (WordMatch.Success)
            {
                Capture wordCapture = WordMatch.Groups[1];
                nextWord = wordCapture.Value;
                offset = wordCapture.Index + nextWord.Length;
            }


            return (nextWord, offset);
        }

        public static (string nextNum, int offset) GetNextNumInString(string toParse)
        {
            Regex NextNumRegex = new Regex(@"\s*([\d\.]+)");
            Match FloatMatch = NextNumRegex.Match(toParse);
            string nextNum = "-1";
            int offset = -1;
            if (FloatMatch.Success)
            {
                Capture FloatCapture = FloatMatch.Groups[1];
                nextNum = FloatCapture.Value;
                offset = FloatCapture.Index;
            }
            return (nextNum, offset);
        }

        /// <summary>
        /// Gets the value inbetween the next pair of quotes/apostrophes.
        /// </summary>
        /// <remarks><para>Not multiline.</para></remarks>
        /// <param name="toParse">The string containing the quote pair. The contents between the first pair will be returned.</param>
        /// <returns>
        /// <para>nextString: The contents of the string <b>without</b> the quotes.</para>
        /// <para>stringStart: The position of the <b>opening quote</b>.</para>
        /// <para>stringEnd: The position of the <b>closing quote</b>.</para>
        /// </returns>
        public static (string nextString, int stringStart, int stringEnd) GetNextStringInString(string toParse)
        {
            Regex NextStringRegex = new Regex(@"([""'])(.*?[^\\])\1"); // " or ' is \1, all text between quotes is \2.
            Match foundstring = NextStringRegex.Match(toParse);

            int stringEnd;
            string nextString;
            if (foundstring.Success)
            {
                nextString = foundstring.Groups[2].Value;
                stringEnd = foundstring.Groups[2].Index + nextString.Length;
            }
            else
                throw new Exception("Failed to find a string when there should be one! Probably no closing quotes in the given string.");

            return (nextString, foundstring.Index, stringEnd);
        }


        public static int GetNextTableLength(string toParse)
        {
            int len = -1;
            int openingBraces = 0;
            int closingBraces = 0;
            int skipTo = -1;
            for (int i = 0; i < toParse.Length; i++)
            {
                char character = toParse[i];
                if (i < skipTo)
                {
                    continue;
                }

                if (character == '}')
                    closingBraces++;
                else if (character == '{')
                {
                    openingBraces++;
                }

                if (closingBraces == openingBraces && openingBraces > 0)
                {
                    len = i + 1;
                    break;
                }
            }
            return len;
        }

        /// <summary>
        /// Parses a given Lua file/table into a given (string, obj) Dictionary.
        /// </summary>
        /// <remarks>
        /// <para>Table CANNOT contain: functions, hex values, operations, or use variables for values.</para>
        /// The 'return' keyword at any point in the file/table will cause parsing to stop, so be wary. If you're parsing a file, I recommend a return statement at the end.
        /// </remarks>
        /// <param name="tableString">All text BETWEEN the braces of a table, excluding the {} braces. Files are parsed as tables too, so just slap the file contents in here.</param>
        /// <param name="table">The string obj dict that will act as a table.</param>
        /// <param name="searches">A list of keys to search for (eg. "TopLevelTable.SomeTable.SomeValue"</param>
        /// <param name="parent">The key of the parent table. This lets us search for a given value within an unspecified depth of tables. You may or may not want to touch this.</param>
        /// <param name="baseIndex">Do not use. Used to retain the position in the main string so we can give the index of a value relative to the start of the main string.</param>
        /// <returns><para>Zero based index of the <b>value</b> of the optional 'search' key. Returns the index of the starting special character if the value has one eg. `"` or `{`</para></returns>
        public static (Dictionary<string, int> searchValuePositions, Dictionary<string, int> searchKeyPositions) ParseTable(string tableString, out Dictionary<string, object> table, 
                       List<string> searches = null, string parent = "", int baseIndex = 0)
        {
            if (searches == null)
                searches = new List<string>();
            Dictionary<string, int> searchValuePositions = new Dictionary<string, int>();
            Dictionary<string, int> searchKeyPositions = new Dictionary<string, int>();

            table = new Dictionary<string, object>();
            (string test_word, int test_off) = GetNextWordInString(tableString);
            if (test_off == -1 || test_word == "return")
                return (searchValuePositions, searchKeyPositions);

            int skipTo = -1; // Used to skip past a child's value within our table so we don't end up parsing any tables within the child.


            // While this loop gets every character, we should only parse one character to get the value type, and call a function to get the rest of the value, add the value to the dictionary, then skipTo the end of the value in the string.
            for (int i = 0; i < tableString.Length; i++)
            {
                char character = tableString[i];



                if (i < skipTo || character == '=' || character == ' ' || character == ',' || character == '\n' || character == '\r' || character == '\t')
                {
                    continue;
                }

                // Skip comments
                if (character == '-' && tableString[i + 1] == '-')
                {
                    int nextNL = tableString.Substring(i).IndexOf('\n');
                    if (nextNL != -1)
                    {
                        skipTo = i + nextNL;
                    }
                    continue;
                }

                // Get Key. If return, it's return. If local, get key again. If it's an implicit key, this will actually be the value.
                bool hasKey = !IsNextCharacterAValue(tableString.Substring(i));
                string theKey = ""; int endOfKey;
                int keyPos = 0;
                if (hasKey)
                {
                    (theKey, endOfKey) = GetNextWordInString(tableString.Substring(i));
                    keyPos = i;
                    if (endOfKey != -1)
                    {
                        if (theKey == "return")
                        {
                            break;
                        }
                        i += endOfKey;
                        character = tableString[i];
                        if (theKey == "local")
                        {
                            (theKey, endOfKey) = GetNextWordInString(tableString.Substring(i));
                            keyPos = i;
                            i += endOfKey;
                            character = tableString[i];
                        }
                    }
                    else
                        break;
                }
                else
                {
                    //Console.WriteLine("DEBUG: No key at " + (i + baseIndex));
                    int curKey = 1;
                    while (table.ContainsKey(curKey.ToString()))
                        curKey++;
                    theKey = curKey.ToString();
                    keyPos = i;
                }

                string currentKeyPath = (parent == "" ? theKey : parent + "." + theKey);

                // Get Value
                while (character == '=' || character == ' ' || character == ',' || character == '\n' || character == '\t')
                {
                    i++;
                    character = tableString[i];
                }

                if (char.IsDigit(character))
                {
                    (string numValue, int offset) = GetNextNumInString(tableString.Substring(i));
                    table.Add(theKey, numValue);
                    skipTo = i + (offset + numValue.Length);
                }
                else if (character == '"' || character == '\'')
                {
                    (string childValue, int _, int endOfString) = GetNextStringInString(tableString.Substring(i));
                    endOfString++; // Skip closing quote
                    table.Add(theKey, childValue);
                    skipTo = i + endOfString;
                }
                else if (character == 't' || character == 'f')
                {
                    (string nextBool, int endOfWordPos) = GetNextWordInString(tableString.Substring(i));
                    table.Add(theKey, bool.Parse(nextBool));
                    skipTo = i + endOfWordPos;
                }
                else if (character == '{')
                {
                    // We've just found a child in this table. We need to pause what we're doing and start again but on the child.
                    int tableLen = GetNextTableLength(tableString.Substring(i));
                    (Dictionary<string, int> subValuePositions, Dictionary<string, int> subKeyPositions) = ParseTable(tableString.Substring(i + 1, tableLen-1), out Dictionary<string, object> child, searches, currentKeyPath, baseIndex + i + 1);
                    searchValuePositions = searchValuePositions.Concat(subValuePositions).ToDictionary(kvp=>kvp.Key, kvp=>kvp.Value);
                    searchKeyPositions = searchKeyPositions.Concat(subKeyPositions).ToDictionary(kvp=>kvp.Key, kvp=>kvp.Value);

                    table.Add(theKey, child);
                    skipTo = i + tableLen + 1;
                    //Console.WriteLine($"DEBUG: Adding child table at char index " + (baseIndex + i));

                }

                if (searches.Count > 0)
                {
                    foreach (string thisSearch in searches)
                    {
                        if (currentKeyPath == thisSearch)
                        {
                            searchValuePositions.Add(currentKeyPath, baseIndex + i);
                            searchKeyPositions.Add(currentKeyPath, baseIndex + keyPos);
                            //Console.WriteLine($"RESULT: Found {thisSearch} at {baseIndex + i}");
                        }
                    }
                }

            }
            //Console.WriteLine($"Finished parsing a table. Num of children = {table.Count}");
            return (searchValuePositions, searchKeyPositions);
        }

        public static object GetValueFromTable(Dictionary<string, object> table, string valuePath)
        {
            string[] pathSegments = valuePath.Split('.');
            Dictionary<string, object> curTable = table;
            foreach (string nextTableName in pathSegments)
            {
                if (!curTable.ContainsKey(nextTableName))
                {
                    throw new Exception($"{nextTableName} does not exist in table {curTable}.");
                }
                if (nextTableName == pathSegments[pathSegments.Length - 1])
                {
                    return curTable[nextTableName];
                }
                else
                    curTable = CastObjectToTable(curTable[nextTableName]);
            }
            return null;
        }

        /// <summary>Parses a given Dict (table) as if each key/value was a mod from the installedmods table.</summary>
        /// 
        /// <remarks>
        /// Mod objects should <b>only</b> be used for changing existing mod properties in the installedmods file, and <b>not</b> for creating new ones
        /// as values will be lost when the mod table structure evolves (ie. A new variable that specifies something is added to each mod entry will not be written).
        /// <para>If you need to create a new mod or update its properties, use the Manifest To Table functions instead.</para>
        /// </remarks>
        /// 
        /// <param name="installedmodsTable"></param>
        /// <returns></returns>
        public static List<Mod> GetAllModsFromInstalledModsFileDict(Dictionary<string, object> installedmodsFileTable)
        {
            List<Mod> allInstalledMods = new List<Mod>();
            Dictionary<string, object> installedmodsTable = CastObjectToTable(installedmodsFileTable["installedmods"]);

            // Foreach mod table
            foreach (string ModKey in installedmodsTable.Keys)
            {
                bool modWasConvertedToTable = LuaParsing.TryCastObjectToTable(installedmodsTable[ModKey], out Dictionary<string, object> modTable);
                if (!modWasConvertedToTable)
                {
                    Console.WriteLine("A mod table was not able to be converted to dictionary! : " + ModKey);
                    continue;
                }
                // Foreach property of the table
                string modID = null, startScriptPath = null;
                double version_Major = -1, version_Minor = -1;
                bool installed = false, enabled = false, clearData = false;
                List<string> files = new List<string>();
                foreach (string ModPropertyKey in modTable.Keys)
                {
                    if (ModPropertyKey == "Name")
                        modID = (string)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "StartScriptPath")
                        startScriptPath = (string)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "Version_Major")
                        version_Major = double.Parse((string)modTable[ModPropertyKey]); // erm...
                    else if (ModPropertyKey == "Version_Minor")
                        version_Minor = double.Parse((string)modTable[ModPropertyKey]);
                    else if (ModPropertyKey == "Installed")
                        installed = (bool)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "Enabled")
                        enabled = (bool)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "ClearData")
                        clearData = (bool)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "Files")
                    {
                        Dictionary<string, object> filesTable = LuaParsing.CastObjectToTable(modTable[ModPropertyKey]);
                        foreach (string fileIndexString in filesTable.Keys)
                        {
                            files.Add((string)filesTable[fileIndexString]);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: Manager doesn't recognize key " + ModPropertyKey + " in mod " + ModKey);
                    }
                }
                Mod thisMod = new Mod(modID, startScriptPath, version_Major, version_Minor, installed, enabled, clearData, false, files);
                allInstalledMods.Add(thisMod);
            }

            return allInstalledMods;
        }


        public static string ReplaceNextWordInString(string sourceString, string replaceWith, int wordIndex = 0)
        {
            (string valueAsString, int valueStringEndPos) = GetNextWordInString(sourceString.Substring(wordIndex));
            if (valueStringEndPos == -1)
            {
                return null;
            }
            string stringBeginning = sourceString.Substring(0, wordIndex);
            string stringEnd = sourceString.Substring(wordIndex + valueAsString.Length);
            return stringBeginning + replaceWith + stringEnd;
        }
        public static string ReplaceNextNumInString(string sourceString, string replacementNum, int wordIndex = 0)
        {
            (string numValue, int numOffset) = GetNextNumInString(sourceString.Substring(wordIndex));
            if (numOffset == -1)
                throw new Exception("Failed to find the number to replace!");
            string numAsString = numValue.ToString();
            string numBeginning = sourceString.Substring(0, wordIndex);
            string stringEnd = sourceString.Substring(wordIndex + numAsString.Length);
            return numBeginning + replacementNum.ToString() + stringEnd;

        }
        public static string ReplaceNextStringInString(string sourceString, string replacementString, int stringIndex = 0)
        {
            (string oldString, int relativeStringStart, int relativeStringEnd) = GetNextStringInString(sourceString.Substring(stringIndex));
            if (relativeStringStart == -1)
                throw new Exception($"Failed to find the next string at index {stringIndex} to replace with {replacementString}");
            string finalStringBeginning = sourceString.Substring(0, stringIndex + relativeStringStart + 1); // +1 to skip opening quote

            return finalStringBeginning + replacementString + sourceString.Substring(stringIndex + relativeStringEnd);
        }
        public static string RemoveNextTableInString(string sourceString, int keyIndex = 0)
        {
            (string key, int _) = GetNextWordInString(sourceString.Substring(keyIndex));
            int commaPos = -1;
            int tableLength = GetNextTableLength(sourceString.Substring(keyIndex));
            if (tableLength == -1)
                throw new Exception($"Couldn't get table length in sourceString at {keyIndex}");

            Regex commaRegex = new Regex(@"}\s*,", RegexOptions.Multiline);
            Match commaMatch = commaRegex.Match(sourceString, keyIndex + tableLength - 1);
            if (commaMatch.Success)
            {
                commaPos = commaMatch.Index;
            }

            Regex cleanupSpacesRegex = new Regex($@"\s*{key}", RegexOptions.RightToLeft | RegexOptions.Multiline);
            Match spacesMatch = cleanupSpacesRegex.Match(sourceString, keyIndex + key.Length);
            if (!spacesMatch.Success)
                throw new Exception($"Space trimming regex couldn't find key? {key}");

            int beginningSplit = spacesMatch.Index;
            int endSplit = (commaMatch.Success ? commaPos + commaMatch.Length : keyIndex + tableLength);
            return sourceString.Substring(0, beginningSplit) + sourceString.Substring(endSplit);
        }
        public static string AddTableToInstalledMods(string installedModsContents, string tableToAdd, string ModName)
        {
            (var valuePositions, var keyPositions) = ParseTable(installedModsContents, out Dictionary<string, object> table, new List<string>() { "installedmods", ModName });
            if (!valuePositions.ContainsKey("installedmods"))
                throw new Exception("Failed to find installedmods when adding table to it?");
            if (valuePositions.ContainsKey($"installedmods.{ModName}"))
                throw new Exception($"{ModName} is already in installedmods!");
            int installedModsStart = valuePositions["installedmods"];

            string finalTableString = "\n\t" + tableToAdd + ',';
            installedModsContents = installedModsContents.Insert(installedModsStart + 1, finalTableString);
            return installedModsContents;
        }

    }
    /* This class was meant to be so you could add a list of operations and then do them all at once without having to re-parse the table after each modification, but just parsing the table every time a value changes is fine really.
    public class TableModification
    {
        public TableModification(OperationType Action, string Key, int ValuePosition, object ValueReplacement = null)
        {
            this.Action = Action;
            this.Key = Key;
            this.ValuePosition = ValuePosition;
            this.ValueReplacement = ValueReplacement;
        }

        public int CompareTo(TableModification a, TableModification b)
        {
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            if (a.ValuePosition < b.ValuePosition)
                return -1;
            else
                return 1;

        }

        public enum OperationType
        {
            ACTION_REPLACE_STRING = 0,
            ACTION_REPLACE_NUM = 1,
            ACTION_REPLACE_BOOL = 2,
            ACTION_REMOVE_TABLE = 4,
        }

        public OperationType Action { get { return _action; } set { _action = value; }  }
        OperationType _action;
        public string Key { get { return _key; } set { _key = value; } }
        string _key;
        public int ValuePosition { get { return _valuePosition; } set { _valuePosition = value; } }
        int _valuePosition = -1;
        public object ValueReplacement { get { return _valueReplacement; } set { _valueReplacement = value; } }
        object _valueReplacement;
    }
    */
    public class ModManaging {
        public const string DataFolder = @"C:\CGames\Fable 2 GOTY Modded\data\";
        public const string ScriptsFolder = DataFolder + @"\scripts\";
        public const string ModsFolder = DataFolder + @"scripts\Mods\";
        public const string RunnerFolder = ScriptsFolder + @"\Mod Manager\";
        public const string RunnerVersionFileName = @"runnerversion.txt";

        public const string dirManifestPath = DataFolder + @"\dir.manifest";
        public const string dirManifestBackupPath = dirManifestPath + ".bak";
        public const string dirManifestHash = "C67DD5E9E6C6D433A5F8D88B1CECA8D0D778D71718DC5785D98D79F251E23090";
        public static string CurrentDirManifestContent { get { return _currentDirManifestContent; } set { _currentDirManifestContent = value; } } // TODO: Change this to something safer lol
        static string _currentDirManifestContent = File.ReadAllText(dirManifestPath);

        public static List<Mod> ModList { get { return _modList; } set { _modList = value; } }
        static List<Mod> _modList = new List<Mod>();

        public static string InstalledModsFileContent { get { return _installedModsContent; } set { _installedModsContent = value; } }
        static string _installedModsContent = "";
        public static Dictionary<string, object> InstalledModsFileDict { get { return _installedModsDict; } set { _installedModsDict = value; } }
        static Dictionary<string, object> _installedModsDict = new Dictionary<string, object>();


        /// <summary>
        /// Compares installedmods and loose mods in the Mods folder to see what needs updating/removing.
        /// </summary>
        /// <remarks>Perhaps this method shouldn't exist, and instead mods should only be marked as different when something changes (files are missing, or an intentional act through the manager)</remarks>
        /// <param name="installedmodsDict"></param>
        /// <returns>
        /// <list type="bullet">
        /// <item>outOfDateMods - Mods with a differing version in the folder</item>
        /// <item>neverInstalledMods - Mods that are found in folder but not installedmods</item>
        /// <item>uninstalledMods - Mods that are found within installedmods but not folder</item>
        /// </list>
        /// </returns>
        static void EnumerateAllMods()
        {
            // In the UI we will take the pre-existing installedmodsList from static resources.
            // Or can we bind to static properties? Heard we can.

            List<Mod> installedmodsList = LuaParsing.GetAllModsFromInstalledModsFileDict(InstalledModsFileDict);
            List<Mod> loosemodsList = ManifestParser.ConvertManifestsToMods(ManifestParser.GetAllManifestsInModFolder());
            List<Mod> FinalModList = new List<Mod>();

            Dictionary<string, Mod> installedModsDict = new Dictionary<string, Mod>();

            foreach (Mod installedmod in installedmodsList)
            {
                if (installedModsDict.ContainsKey(installedmod.NameID))
                    throw new Exception("Somehow a mod with the same nameID was found twice! " + installedmod.NameID);
                installedModsDict.Add(installedmod.NameID, installedmod);

                if (!File.Exists(ModsFolder + installedmod.NameID))
                    installedmod.IsDeleted = true;
                FinalModList.Add(installedmod);
            }
                
            foreach (Mod loosemod in loosemodsList)
            {
                if (installedModsDict.ContainsKey(loosemod.NameID))
                {
                    Mod installedmod = installedModsDict[loosemod.NameID];
                    // Found existing mod
                    if (installedmod.VersionMajor != loosemod.VersionMajor || installedmod.VersionMinor != loosemod.VersionMinor)
                    {
                        installedmod.IsOutOfDate = true;
                        Console.WriteLine("Versions differ for mod: " + installedmod.NameID);
                    }
                }
                else
                {
                    loosemod.IsNew = true;
                    FinalModList.Add(loosemod);
                }

            }

            ModList = FinalModList;
            // We now have every mod processed from both lists, let's concat them while making sure there's no duplicates and that they retain their management status'

        }

        static void UpdateAllOutOfDateMods()
        {
            EnumerateAllMods();

            foreach (Mod oldMod in ModList)
            {
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

        public static string AddModFilesToDirManifest(Mod mod, string manifestContent)
        {
            // ISSUE: THIS IS ADDING FILES FROM OLD MODS(?)
            //List<string> dirManifestLines = File.ReadAllLines(dirManifestPath).ToList<string>();
            List<string> dirManifestLines = manifestContent.Split(
                                                new string[] { "\r\n", "\r", "\n" },
                                                StringSplitOptions.None
                                            ).ToList();
            Dictionary<string, bool> manifestAsDict = new Dictionary<string, bool>();
            List<string> newModLines = new List<string>();
            foreach (string thisEntry in dirManifestLines)
            {
                if (string.IsNullOrEmpty(thisEntry))
                    continue;
                manifestAsDict.Add(thisEntry.Replace('/', '\\'), true);
            }
            foreach (string modFile in mod.Files)
            {
                string modFileTrimmed = modFile.Trim().Replace('/','\\');
                if (manifestAsDict.ContainsKey(modFileTrimmed))
                    Console.WriteLine($"{mod.NameID}'s file is already in dir.manifest:\n\t`{modFileTrimmed}`");
                else
                    newModLines.Add(modFileTrimmed);
            }
            dirManifestLines = dirManifestLines.Concat(newModLines).ToList();
            //File.WriteAllLines(dirManifestPath + ".txt", dirManifestLines);
            return string.Join("\n", dirManifestLines);
        }
        public static void RemoveModFilesFromDirManifest(Mod mod, string manifestPath = dirManifestPath)
        {
            List<string> dirManifestLines = File.ReadAllLines(manifestPath).ToList<string>();
            Dictionary<string, int> manifestAsDict = new Dictionary<string, int>();
            List<string> newModLines = new List<string>();
            int _i = 0;
            foreach (string thisEntry in dirManifestLines)
            {
                manifestAsDict.Add(thisEntry.Replace('/', '\\').Trim(), _i);
                _i++;
            } _i = 0;
            foreach (string modFile in mod.Files)
            {
                string modFileTrimmed = modFile.Trim().Replace('/', '\\');
                if (!dirManifestLines.Remove(modFileTrimmed))
                    Console.WriteLine($"Failed to remove file from dir.manifest:\n\t `{modFileTrimmed}`");
            }
            dirManifestLines = dirManifestLines.Concat(newModLines).ToList();
            File.WriteAllLines(dirManifestPath + ".txt", dirManifestLines);
        }
        public static string RemoveFilesFromDirManifest(List<string> filesToRemove, string manifestContent)
        {
            List<string> dirManifestLines = manifestContent.Split(new char[] { '\n', '\r' }).ToList();
            dirManifestLines.RemoveAll(x => filesToRemove.Contains(x));
            return string.Join("\n", dirManifestLines);
        }
        public static List<string>DirManifestDelta(string manifestPath = dirManifestPath, string backupPath = dirManifestBackupPath)
        {
            List<string> manFiles = File.ReadAllLines(manifestPath).ToList();
            Dictionary<string, string> backFilesDict = File.ReadAllLines(backupPath).ToDictionary<string, string>(x => x);
            List<string> delta = manFiles.Where((file, index) => !backFilesDict.ContainsKey(file)).ToList();

            return delta;
        }
        public static bool VerifyDirManifestBackup(string manifestPath = dirManifestBackupPath)
        {
            using (SHA256 sha = SHA256.Create())
            {
                using (FileStream stream = File.OpenRead(manifestPath))
                {
                    byte[] curFileHashBytes = sha.ComputeHash(stream);
                    string curFileHashString = string.Join("", curFileHashBytes.Select(x => x.ToString("X2")));
                    Console.WriteLine($"Current manifest: \n\t{curFileHashString}\nOrigin: {dirManifestHash}");
                    return curFileHashString == dirManifestHash;
                }
            }
        }
        public static string RestoreOriginalDirManifest()
        {
            //throw new NotImplementedException("You should make sure this code is correct before calling this properly :p");
            File.Copy(dirManifestBackupPath, dirManifestPath, true);
            return File.ReadAllText(dirManifestPath);
        }
        public static void ModifyFreshDirManifest()
        {
            CurrentDirManifestContent = RestoreOriginalDirManifest();
            foreach (Mod mod in ModList)
            {
                CurrentDirManifestContent = AddModFilesToDirManifest(mod, CurrentDirManifestContent);
            }
            File.WriteAllText(dirManifestPath, CurrentDirManifestContent);
        }

        static string GetRunnerVersion()
        {
            string RunnerVersionPath = RunnerFolder + RunnerVersionFileName;
            if (!File.Exists(RunnerVersionPath))
                throw new Exception($"Failed to find the runnerversion file at\n`{RunnerVersionPath}`");
            string[] fileLines = File.ReadAllLines(RunnerFolder + RunnerVersionFileName);
            if (fileLines.Length <= 0)
                throw new Exception("runnerversion file is empty! (no lines)");

            string versionText = fileLines[0].Trim();
            if (string.IsNullOrWhiteSpace(versionText))
                throw new Exception("runnerversion file is empty! (just whitespace)");

            return versionText;
        }


        static (Dictionary<string, object> table, string newTableText) ModifyInstalledModsWithRandomStuff(string fileText)
        {
            //fileText = File.ReadAllText(Mod.InstalledModsPath);

            // Try and change a bool in the file
            string boolToFind = "installedmods.TestMod.Enabled";
            string numToFind = "installedmods.TestMod.Version_Major";
            string tableToFind = "installedmods.WispFriend";
            string stringToFind = "installedmods.RuntimeCodeRunner.InconsequentialString";
            (var foundValuePositions, var foundKeyPositions) = LuaParsing.ParseTable(fileText, out Dictionary<string, object> mytable, new List<string>() { boolToFind});
            if (!foundValuePositions.TryGetValue(boolToFind, out int boolOffset))
                throw new Exception("didn't find bool lol");
            fileText = LuaParsing.ReplaceNextWordInString(fileText, "true", boolOffset);
            // Try and change a num
            (foundValuePositions, foundKeyPositions) = LuaParsing.ParseTable(fileText, out mytable, new List<string>() {numToFind});
            if (!foundValuePositions.TryGetValue(numToFind, out int numOffset))
                throw new Exception("didn't find num lol");
            fileText = LuaParsing.ReplaceNextNumInString(fileText, "2", numOffset);
            // Try and remove a table            
            (foundValuePositions, foundKeyPositions) = LuaParsing.ParseTable(fileText, out mytable, new List<string>() { tableToFind });
            if (!foundKeyPositions.TryGetValue(tableToFind, out int tableKeyOffset))
                throw new Exception("didn't find table lol");
            fileText = LuaParsing.RemoveNextTableInString(fileText, tableKeyOffset);
            // Try and Change a string
            (foundValuePositions, foundKeyPositions) = LuaParsing.ParseTable(fileText, out mytable, new List<string>() { stringToFind});
            if (!foundValuePositions.TryGetValue(stringToFind, out int stringOffset))
                throw new Exception("didn't find string lol");
            fileText = LuaParsing.ReplaceNextStringInString(fileText, "EvenBetterCodeRunner", stringOffset);

            // Update runner version:
            mytable.TryGetValue("runnerversion", out object versionInTable);
            string versionInFile = GetRunnerVersion();
            bool versionSame = (string)versionInTable == versionInFile;
            Console.WriteLine($"version same: {versionSame}");
            if (!versionSame)
            {
                (var valuepos, var keypos) = LuaParsing.ParseTable(fileText, out mytable, new List<string> { "runnerversion" });
                fileText = LuaParsing.ReplaceNextNumInString(fileText, versionInFile, valuepos["runnerversion"]);
            }

            //fileText = LuaTableParser.AddTableToInstalledMods(fileText, "bigsquares = {big = true, circles = false, amount = 3, someStr = \"some string here\"}");

            return (mytable, fileText);
        }


        static void Main(string[] args)
        {
            ManifestParser.JsonOptions = new JsonSerializerOptions();
            ManifestParser.JsonOptions.AllowTrailingCommas = true;
            ManifestParser.JsonOptions.WriteIndented = true;

            Console.BufferHeight = Console.LargestWindowHeight;
            Console.WindowHeight = (int)Math.Floor(Console.LargestWindowHeight/1.5f);
            // Table modification for installedmods
            //(var returnedTable, string modifiedLuaText) = ModifyInstalledModsWithRandomStuff();
            string fileText = File.ReadAllText(Mod.InstalledModsPath);

            LuaParsing.ParseTable(fileText, out var installedModsDict);
            InstalledModsFileDict = installedModsDict;
            InstalledModsFileContent = fileText;

            ModManaging.UpdateAllOutOfDateMods();




            foreach(Mod mod in ModList)
            {
                string printString = "\t-------";
                printString += "\tMOD - " + mod.NameID + "\n";
                printString += "Enabled: " + mod.Enabled.ToString() + "\n";
                printString += "Installed: " + mod.Installed.ToString() + "\n";
                printString += "ClearData: " + mod.ClearData.ToString() + "\n";
                printString += "New: " + mod.IsNew.ToString() + "\n";
                printString += "FromManifest: " + mod.IsFromManifest.ToString() + "\n";
                printString += "Missing Manifest: " + mod.IsDeleted.ToString() + "\n";
                printString += "\n";
                Console.Write(printString);
            }
            using (SHA256 sha = SHA256.Create())
            {
                FileInfo finfo = new FileInfo(dirManifestPath);
                using (Stream stream = finfo.OpenRead())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        Console.Write($"{hash[i]:X2}");
                    }
                    Console.WriteLine();
                }
            }

            var delta = DirManifestDelta();
            foreach (string str in delta)
                Console.WriteLine("Differing file: " + str);
            //Console.WriteLine($"Current backup is original: {VerifyDirManifestBackup()}");
            ModifyFreshDirManifest();
            File.WriteAllText(@"C:\CGames\Fable 2 GOTY Modded\data\scripts\Mod Manager\installedmodsparsed.lua", InstalledModsFileContent);
            //File.WriteAllText(dirManifestPath, currentDirManifestContent);
        }
    }

    static class ManifestParser
    {
        public static string ModManifestFilename = "modmanifest.json";

        public static JsonSerializerOptions JsonOptions { get; set; }

        /// <summary>
        /// FOR TESTING ONLY. Manifests may be created programmatically, but the Mod structure is immutable while manifests/tables are not, so new properties will be lost. A seperate tool might be created for this kind of thing.
        /// </summary>
        /// <param name="mod"></param>
        /// <returns></returns>
        public static string CreateModManifest(Mod mod)
        {
            string modManifest = JsonSerializer.Serialize(mod, typeof(Mod), JsonOptions);

            return modManifest;
        }

        public static Mod ConvertManifestToMod(string manifestText)
        {
            Mod mod = (Mod)JsonSerializer.Deserialize(manifestText, typeof(Mod), JsonOptions);
            mod.IsFromManifest = true;
            return mod;
        }

        public static List<string> GetAllManifestsInModFolder()
        {
            List<string> manifests = new List<string>();

            foreach (string folder in Directory.EnumerateDirectories(ModManaging.ModsFolder))
            {
                string finalFolder = folder + '\\';
                string manPath = finalFolder + ModManifestFilename;
                if (File.Exists(manPath))
                {
                    manifests.Add(File.ReadAllText(manPath));
                }
                else Console.WriteLine("No manifest in folder: " + finalFolder);
            }

            return manifests;
        }

        public static string GetManifestFromModFolder(string ModID)
        {
            string manPath = ModManaging.ModsFolder + ModID + "\\" + ModManifestFilename;
            if (File.Exists(manPath))
            {
                return File.ReadAllText(manPath);
            }
            else throw new Exception("Failed to find manifest for mod " + ModID);
        }

        public static List<Mod> ConvertManifestsToMods(List<string> manifests)
        {
            List<Mod> mods = new List<Mod>();

            foreach (string manifest in manifests)
            {
                Mod mod = ConvertManifestToMod(manifest);
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

            string finalTable = "{ ";
            string modKey;

            JsonDocumentOptions docOpts = new JsonDocumentOptions();
            docOpts.AllowTrailingCommas = true;
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
            
            if (addConfigValues)
            {
                finalTable += " Enabled = false, Installed = false, ClearData = false,\n";
            }

            finalTable += "}";

            return $"{modKey} = {finalTable}";
        }

        static (string tableContent, string modKey) ParseJsonObjectIntoLuaTable(JsonElement element, int depth)
        {
            string tableContent = "";
            string modKey = "error_noname";
            string unhandledValue = "";

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    tableContent += $"\n{Indent(depth)}{property.Name} = {{ {ParseJsonObjectIntoLuaTable(property.Value, depth + 1)} }},\n";
                }
                else if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    // Files array probably
                    tableContent += $"\n{Indent(depth)}{property.Name} = {{\n";
                    foreach (JsonElement arrayElement in property.Value.EnumerateArray())
                    {
                        tableContent += $"{Indent(depth + 1)}\"{arrayElement.GetString()}\",\n";
                    }
                    tableContent += $"{Indent(depth + 1)}}},\n";
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                    tableContent += $"{property.Name} = \"{property.Value.GetString()}\", ";
                else if (property.Value.ValueKind == JsonValueKind.Number)
                    tableContent += $"{property.Name} = {property.Value.GetDouble()}, ";
                else
                    unhandledValue = property.Name;

                if (property.Name == "NameID")
                    modKey = property.Value.GetString();
            }
            if (!string.IsNullOrEmpty(unhandledValue))
                Console.WriteLine($"Unhandled value type in mod {modKey} with key {unhandledValue}");
            return (tableContent, modKey);
        }

        static string Indent(int times)
        {
            string indentation = "";
            char indentChar = '\t';
            for (int i = 0; i < times; i++)
            {
                indentation += indentChar;
            }
            return indentation;
        }
    }

    public class Mod
    {
        public const string InstalledModsPath = @"C:\CGames\Fable 2 GOTY Modded\data\scripts\Mod Manager\installedmods.lua";

        // Should be called by the setter
        public void SetModBool(string property, bool value)
        {
            string propertyPath = $"installedmods.{this.NameID}.{property}";
            (var values, var _) = LuaParsing.ParseTable(ModManaging.InstalledModsFileContent, out Dictionary<string, object> newTable, new List<string> {propertyPath});
            if (!values.ContainsKey(propertyPath))
                throw new Exception("Couldn't toggle enabled on mod " + this.NameID);

            ModManaging.InstalledModsFileDict = newTable;
            ModManaging.InstalledModsFileContent = LuaParsing.ReplaceNextWordInString(ModManaging.InstalledModsFileContent, value.ToString().ToLower(), values[propertyPath]);
        }

        public void InstallModIntoInstalledMods(string installedModsContents)
        {
            Installed = true;
            ModManaging.InstalledModsFileContent = LuaParsing.AddTableToInstalledMods(installedModsContents, ManifestParser.ConvertJsonManifestIntoTable(ManifestParser.GetManifestFromModFolder(NameID)), NameID);
        }

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
            (foundBoolOffsets, foundKeyOffsets) = LuaParsing.ParseTable(ModManaging.InstalledModsFileContent, out var installedModsDict, new List<string>() { modPath });
            if (foundKeyOffsets.Count <= 0)
                throw new Exception("Failed to find Mod " + NameID + " in installedmods to remove for updating!");

            // Remove old mod files and add new ones
            {
                var oldModFiles = Files;
                Mod newMod = ManifestParser.ConvertManifestToMod(newManifest);
                var newModfiles = newMod.Files;
                var redundantFiles = new List<string>();
                redundantFiles.AddRange(oldModFiles.Where(x => !newModfiles.Contains(x)));
                Console.WriteLine($"DEBUG: Removing files from {NameID}: `{redundantFiles.Count}`");

                ModManaging.CurrentDirManifestContent = ModManaging.RemoveFilesFromDirManifest(redundantFiles, ModManaging.CurrentDirManifestContent);
                ModManaging.CurrentDirManifestContent = ModManaging.AddModFilesToDirManifest(newMod, ModManaging.CurrentDirManifestContent);
                Files = oldModFiles;
            }

            // Remove old mod table and insert the new one
            ModManaging.InstalledModsFileContent = LuaParsing.RemoveNextTableInString(ModManaging.InstalledModsFileContent, foundKeyOffsets[modPath]);
            ModManaging.InstalledModsFileContent = LuaParsing.AddTableToInstalledMods(ModManaging.InstalledModsFileContent, newModTableString, NameID);

            LuaParsing.ParseTable(ModManaging.InstalledModsFileContent, out installedModsDict); // Reassign the new installedmods dictionary containing the new mod version to the public property
            ModManaging.InstalledModsFileDict = installedModsDict;
        }

        public Mod(string NameID, string StartScriptPath, double VersionMajor, double VersionMinor, bool Installed, bool Enabled, bool ClearData, bool IsFromManifest, List<string> Files)
        {
            if (NameID == null || StartScriptPath == null || Files == null)
                throw new Exception($"Tried to instantiate mod but something is null:\nname={NameID}, startscript={StartScriptPath}, files={Files}");
            else if (Files.Count == 0)
                Console.WriteLine("Files for mod " + NameID + " is empty. There should probably be at least 1 file.");


            this._nameID = NameID;
            this._startScriptPath = StartScriptPath;
            this._versionMajor = VersionMajor;
            this._versionMinor = VersionMinor;
            this._installed = Installed;
            this._enabled = Enabled;
            this._clearData = ClearData;
            this._isFromManifest = IsFromManifest;
            this._files = Files;
        }

        public override string ToString()
        {
            string nameString = NameID ?? "NULLMODNAME";
            return nameString;
        }

        public string NameID { get { return _nameID; } set{ _nameID = value; } }
        private string _nameID;
        public string StartScriptPath { get { return _startScriptPath; } set { _startScriptPath = value; } }
        private string _startScriptPath;
        public double VersionMajor { get { return _versionMajor; } set { _versionMajor = value; } }
        private double _versionMajor;
        public double VersionMinor { get { return _versionMinor; } set { _versionMinor = value; } }
        private double _versionMinor;
        public bool Installed { get { return _installed; } set { 
                _installed = value;
                SetModBool("Installed", value);
            } }
        private bool _installed;
        public bool Enabled { get { return _enabled; } set { 
                _enabled = value;
                SetModBool("Enabled", value);
            } }
        private bool _enabled;
        public bool ClearData { get { return _clearData; } set { 
                _clearData = value;
                SetModBool("ClearData", value);
            } }
        private bool _clearData;

        public List<string> Files { get { return _files; } set { _files = value; } }
        private List<string> _files;

        public bool IsNew { get { return _isNew; } set { _isNew = value; } }
        private bool _isNew;
        public bool IsOutOfDate { get { return _isOutOfDate; } set { _isOutOfDate = value; } }
        private bool _isOutOfDate;
        public bool IsDeleted { get { return _isDeleted; } set { _isDeleted= value; } }
        private bool _isDeleted;

        public bool IsFromManifest { get { return _isFromManifest; } set { _isFromManifest = value; } }
        private bool _isFromManifest;

        
    }
}