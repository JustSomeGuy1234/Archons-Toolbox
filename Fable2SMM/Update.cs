using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using System.Windows;

namespace ArchonsToolbox
{
    static class Update
    {
        public const string CurrentVersion = "1.0";
        public const string UpdateURL = "https://github.com/JustSomeGuy1234/Fable2SMM/releases/latest";
        public static string GetLatestVersion()
        {
            try
            {
                HttpWebRequest httpWebRequest = WebRequest.CreateHttp(UpdateURL);
                httpWebRequest.Method = "GET";
                string originalString = httpWebRequest.GetResponse().ResponseUri.OriginalString;
                return originalString.Substring(originalString.LastIndexOf("/"[0]) + 1);
            } catch (Exception e)
            {
                MessageBox.Show("Failed to check for updates.");
                Trace.WriteLine("Failed to check for updates:\n\n" + e.Message);
                return "";
            }
		}
    }
}
