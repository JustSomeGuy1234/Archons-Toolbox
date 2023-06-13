using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Windows;

namespace Fable2SMM.HelpSystem
{
    public static class HelpManager
    {
        public static string HelpItemsPath = @".\resources\HelpItems.json";
        public static List<HelpItem> HelpItems { get => _helpItems; set => _helpItems = value; }
        public static List<HelpItem> _helpItems = new List<HelpItem>();

        public static void UpdateHelpItems()
        {
            HelpItems.Clear();
            var items = GetHelpItems();
            if (items != null)
                HelpItems.AddRange(items);
        }

        public static string TESTCreateItems()
        {
            List<HelpItem> items = new List<HelpItem>();
            items.Add(new HelpItem("Test 1", "Description 1"));
            items.Add(new HelpItem("Test 2", "Description 2"));
            return JsonSerializer.Serialize(items, new JsonSerializerOptions() { AllowTrailingCommas = true, WriteIndented = true });
        }

        static List<HelpItem> GetHelpItems()
        {
            if (File.Exists(HelpItemsPath))
            {
                using (Stream stream = File.OpenRead(HelpItemsPath))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<HelpItem>>(stream, new JsonSerializerOptions { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip });
                    }
                    catch ( Exception e )
                    {
                        Trace.TraceError("Error: Failed to deserialize the HelpItems file:\n" + e.Message);
                        MessageBox.Show("Couldn't parse help data.\n\nRedownload the manager to fix this.", "Missing File", MessageBoxButton.OK, MessageBoxImage.Hand);
                    }
                }
            }
            else
            {
                MessageBox.Show("Couldn't find the help file.\n\nRedownload the manager to fix this.", "Missing File", MessageBoxButton.OK, MessageBoxImage.Hand);
                Trace.TraceError("Error: couldn't find help items file");
            }
            return null;
        }
    }
}
