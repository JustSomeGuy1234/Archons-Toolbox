using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Diagnostics;

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
                return !char.IsLetter(nextCharacter);
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
                throw new Exception("LuaParsing: Failed to find a string when there should be one! Probably no closing quotes in the given string.");

            return (nextString, foundstring.Index, stringEnd);
        }

        public static string ReadInstalledModsIntoContentAndDict()
        {
            if (!File.Exists(Mod.InstalledModsPath))
                return string.Empty; // TODO: A neat way to show a messagebox and return?
            string installedModsContent = File.ReadAllText(Mod.InstalledModsPath);
            ModManaging.CurrentInstalledModsContent = installedModsContent;
            ParseTable(installedModsContent, out var installedModsDict);
            ModManaging.InstalledModsFileDict = installedModsDict;
            return installedModsContent;
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

        /// <summary>Parses a given Lua file/table into a given (string, obj) Dictionary.</summary>
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
                    (Dictionary<string, int> subValuePositions, Dictionary<string, int> subKeyPositions) = ParseTable(tableString.Substring(i + 1, tableLen - 1), out Dictionary<string, object> child, searches, currentKeyPath, baseIndex + i + 1);
                    searchValuePositions = searchValuePositions.Concat(subValuePositions).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    searchKeyPositions = searchKeyPositions.Concat(subKeyPositions).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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
                    throw new Exception($"LuaParsing: {nextTableName} does not exist in table {curTable}.");
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
            if (!installedmodsFileTable.ContainsKey("installedmods"))
                throw new Exception("LuaParsing: No installedmods table in installedmods string???");
            Dictionary<string, object> installedmodsTable = CastObjectToTable(installedmodsFileTable["installedmods"]);

            // Foreach mod table
            foreach (string ModKey in installedmodsTable.Keys)
            {
                bool modWasConvertedToTable = LuaParsing.TryCastObjectToTable(installedmodsTable[ModKey], out Dictionary<string, object> modTable);
                if (!modWasConvertedToTable)
                {
                    System.Diagnostics.Trace.WriteLine("A mod table was not able to be converted to dictionary! : " + ModKey);
                    continue;
                }
                // Foreach property of the table
                string modID = null, startScriptPath = null, description = "", author = "";
                List<string> authorURLs = new List<string>();
                double version_Major = -1, version_Minor = -1;
                bool installed = false, enabled = false, clearData = false;
                List<string> files = new List<string>();
                foreach (string ModPropertyKey in modTable.Keys)
                {
                    if (ModPropertyKey == "NameID")
                        modID = (string)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "StartScriptPath")
                        startScriptPath = (string)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "VersionMajor")
                        version_Major = double.Parse((string)modTable[ModPropertyKey]); // erm...
                    else if (ModPropertyKey == "VersionMinor")
                        version_Minor = double.Parse((string)modTable[ModPropertyKey]);
                    else if (ModPropertyKey == "Installed")
                        installed = (bool)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "Enabled")
                        enabled = (bool)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "ClearData")
                        clearData = (bool)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "Description")
                        description = (string)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "Author")
                        author = (string)modTable[ModPropertyKey];
                    else if (ModPropertyKey == "Files")
                    {
                        Dictionary<string, object> filesTable = LuaParsing.CastObjectToTable(modTable[ModPropertyKey]);
                        foreach (string fileIndexString in filesTable.Keys)
                        {
                            files.Add((string)filesTable[fileIndexString]);
                        }
                    }
                    else if (ModPropertyKey == "AuthorURLs")
                    {
                        Dictionary<string, object> authorURLsTable = LuaParsing.CastObjectToTable(modTable[ModPropertyKey]);
                        foreach (string authorIndexString in authorURLsTable.Keys)
                        {
                            authorURLs.Add((string)authorURLsTable[authorIndexString]);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("Warning: Manager doesn't recognize key " + ModPropertyKey + " in mod " + ModKey);
                    }
                }
                Mod thisMod = new Mod(modID, startScriptPath, version_Major, version_Minor, installed, enabled, clearData, files, description, author, authorURLs, true);
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
                throw new Exception("LuaParsing: Failed to find the number to replace!");
            string numAsString = numValue.ToString();
            string numBeginning = sourceString.Substring(0, wordIndex);
            string stringEnd = sourceString.Substring(wordIndex + numAsString.Length);
            return numBeginning + replacementNum.ToString() + stringEnd;

        }
        public static string ReplaceNextStringInString(string sourceString, string replacementString, int stringIndex = 0)
        {
            (string _, int relativeStringStart, int relativeStringEnd) = GetNextStringInString(sourceString.Substring(stringIndex));
            if (relativeStringStart == -1)
                throw new Exception($"LuaParsing: Failed to find the next string at index {stringIndex} to replace with {replacementString}");
            string finalStringBeginning = sourceString.Substring(0, stringIndex + relativeStringStart + 1); // +1 to skip opening quote

            return finalStringBeginning + replacementString + sourceString.Substring(stringIndex + relativeStringEnd);
        }
        public static string RemoveNextTableInString(string sourceString, int keyIndex = 0)
        {
            (string key, int _) = GetNextWordInString(sourceString.Substring(keyIndex));
            int commaPos = -1;
            int tableLength = GetNextTableLength(sourceString.Substring(keyIndex));
            if (tableLength == -1)
                throw new Exception($"LuaParsing: Couldn't get table length in sourceString at {keyIndex}");

            Regex commaRegex = new Regex(@"}\s*,", RegexOptions.Multiline);
            Match commaMatch = commaRegex.Match(sourceString, keyIndex + tableLength - 1);
            if (commaMatch.Success)
            {
                commaPos = commaMatch.Index;
            }

            Regex cleanupSpacesRegex = new Regex($@"\s*{key}", RegexOptions.RightToLeft | RegexOptions.Multiline);
            Match spacesMatch = cleanupSpacesRegex.Match(sourceString, keyIndex + key.Length);
            if (!spacesMatch.Success)
                throw new Exception($"LuaParsing: Space trimming regex couldn't find key? {key}");

            int beginningSplit = spacesMatch.Index;
            int endSplit = (commaMatch.Success ? commaPos + commaMatch.Length : keyIndex + tableLength);
            return sourceString.Substring(0, beginningSplit) + sourceString.Substring(endSplit);
        }
        public static string AddTableToInstalledMods(string installedModsContents, string tableToAdd, string ModName)
        {
            if (string.IsNullOrEmpty(installedModsContents))
            {
                string logerror = "AddTableToInstalledMods: installedModsContent is null or empty!";
                string humanerror = "Failed to add mod to installedmods.lua - Check manager.log for more.\n\nThis may happen if you haven't installed the manager properly, or you've chosen the wrong game folder entirely.";
                Trace.TraceError(logerror);
                MessageBox.Show(humanerror, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                return "";
            }

            (var valuePositions, var _) = ParseTable(installedModsContents, out Dictionary<string, object> _, new List<string>() { "installedmods", ModName });
            if (!valuePositions.ContainsKey("installedmods"))
                throw new Exception("LuaParsing: Failed to find installedmods when adding table to it?");
            if (valuePositions.ContainsKey($"installedmods.{ModName}"))
                throw new Exception($"LuaParsing: {ModName} is already in installedmods!");
            int installedModsStart = valuePositions["installedmods"];

            string finalTableString = "\n\t" + tableToAdd + ',';
            installedModsContents = installedModsContents.Insert(installedModsStart + 1, finalTableString);
            return installedModsContents;
        }
        public static bool IsModInInstalledMods(string ModName)
        {
            // TODO: Maybe instead just look at installedmods dict instead of parsing the string again?
            (var valPositions, var _) = ParseTable(ModManaging.CurrentInstalledModsContent, out var _, new List<string> { "installedmods." + ModName });
            return valPositions.Count > 0;
        }
    }
}
