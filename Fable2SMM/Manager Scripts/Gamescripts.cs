using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ArchonsToolbox
{
    public class Gamescripts
    {
        public static bool IsGoTY = false;

        public static string GamescriptsPath => Path.Combine(ManagerInstallation.DataFolder + @"gamescripts_r.bnk");
        public static string GamescriptsBackupPath => Path.Combine(GamescriptsPath + ".bak");

        public const string GamescriptsV10OriginalHash = "68A2EF1F703C3C325F268EDCD5CDADBC5C869E96CF7C7E2E38BC017153C876E2";
        public const string GamescriptsV10ManagerHash = "7234BCCD80CA29CC023DC324D1C92E4D243CB7BD7069FAF6F134F697011042FE";

        public const string GamescriptsV1OriginalHash = "15BC606D879145B5ABAF2939857C891CD8C22B495643AD79FA8C81173370DD92";
        public const string GamescriptsV1ManagerHash = "A94E7A64F32C86E96760F79BDD0606B81736B9FFE34068140EFB9321E9AD7366";

        public static GamescriptsStatus CurrentGamescriptsStatus { get => _gamescriptsStatus; set { _gamescriptsStatus = value; CurrentGamescriptsStatusChanged?.Invoke(null, EventArgs.Empty); } }
        private static GamescriptsStatus _gamescriptsStatus = GamescriptsStatus.ORIGINAL;
        public static event EventHandler CurrentGamescriptsStatusChanged;

        public static void UpdateGamescriptsStatus()
        {
            string currentGamescriptsHash = GetFileHash(Gamescripts.GamescriptsPath);
            Gamescripts.IsGoTY = currentGamescriptsHash == Gamescripts.GamescriptsV10OriginalHash || currentGamescriptsHash == Gamescripts.GamescriptsV10ManagerHash;
            Trace.WriteLine("Is GoTY: " + Gamescripts.IsGoTY.ToString());

            if (currentGamescriptsHash == Gamescripts.GamescriptsV10ManagerHash || currentGamescriptsHash == Gamescripts.GamescriptsV1ManagerHash)
                Gamescripts.CurrentGamescriptsStatus = GamescriptsStatus.MANAGERINSTALLED;
            else if (currentGamescriptsHash == Gamescripts.GamescriptsV10OriginalHash || currentGamescriptsHash == Gamescripts.GamescriptsV1OriginalHash)
                Gamescripts.CurrentGamescriptsStatus = GamescriptsStatus.ORIGINAL;
            else if (!string.IsNullOrEmpty(currentGamescriptsHash))
                Gamescripts.CurrentGamescriptsStatus = GamescriptsStatus.MODIFIED;
            else
                Gamescripts.CurrentGamescriptsStatus = GamescriptsStatus.MISSING;
        }
        public static string GetFileHash(string filePath)
        {
            string curFileHashString;
            using (SHA256 sha = SHA256.Create())
            {
                if (File.Exists(filePath))
                {
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        byte[] curFileHashBytes = sha.ComputeHash(stream);
                        curFileHashString = string.Join("", curFileHashBytes.Select(x => x.ToString("X2")));
                    }
                }
                else
                    return "";
            }
            return curFileHashString;
        }
    }
}
