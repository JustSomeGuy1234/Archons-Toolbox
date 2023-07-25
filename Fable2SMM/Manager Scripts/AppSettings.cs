using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Diagnostics;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fable2SMM
{
    class AppSettings
    {
        public static bool StartingUp = true;
        public static bool SettingsAreDirty
        {
            get;
            set;
        }

        public const string ResourcesPath = "./resources/";
        public static string SettingsPath { get { return ResourcesPath + "settings.json"; } }
        public static void LoadManagerSettings()
        {
            if (!Directory.Exists(ResourcesPath))
                Directory.CreateDirectory(ResourcesPath);
            if (File.Exists(SettingsPath))
            {
                // Load settings
                try
                {
                    using (FileStream stream = File.OpenRead(SettingsPath))
                    {
                        // This goes far beyond "load manager settings". This is basically the entire loading process for installed mods, and any errors will be thrown here if not caught.
                        AppSettings loadedSettings = JsonSerializer.Deserialize<AppSettings>(stream, ManifestParser.JsonOptions);
                        Inst = loadedSettings ?? new AppSettings(GamePath: "", HasShownOOBE: false, FontSizeMulti: 1.25, BackgroundColour: null, TextColour: null,
                            MainWindowHeight: DefaultMainWindowHeight, MainWindowWidth: DefaultMainWindowWidth);
                    }
                }
                catch (Exception e)
                {
                    var result = MessageBox.Show("Failed to load config or mods from game folder.\nReason:\n\n" + e.Message +
                        "\n\nIt's highly recommended you reset your settings (Game location, font colour, etc). This will not delete any mods or data.\n\nReset?",
                        "Failed to Load Settings", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (result == MessageBoxResult.Yes)
                        CreateNewSettingsFile();
                }
            } else
                CreateNewSettingsFile();
            AppSettings.StartingUp = false;
        }

        public static void CreateNewSettingsFile()
        {
            Inst = new AppSettings(GamePath: "", HasShownOOBE: false, FontSizeMulti: 1.25, BackgroundColour: null, TextColour: null,
                                   MainWindowHeight: DefaultMainWindowHeight, MainWindowWidth: DefaultMainWindowWidth);
            string serialized = JsonSerializer.Serialize<AppSettings>(Inst);
            File.WriteAllText(SettingsPath, serialized);
        }

        public AppSettings(string GamePath, bool HasShownOOBE, double FontSizeMulti, ObservableCollection<ColourByte> BackgroundColour, ObservableCollection<ColourByte> TextColour, double MainWindowHeight, double MainWindowWidth)
        {
            try
            {
                this.GamePath = GamePath;
            } catch(Exception e)
            {

                string err = "Error while changing game path:\n" + e.Message + "\n" + e.StackTrace;
                Trace.TraceError(err);
                MessageBox.Show(err);
                this.GamePath = "";
            }
            this._hasShownOOBE = HasShownOOBE;
            this.FontSizeMulti = FontSizeMulti;
            this.BackgroundColour = BackgroundColour ?? new ObservableCollection<ColourByte> {
                new ColourByte(0, ColourByte.ByteEventEnum.UPDATEBACKGROUND), new ColourByte(0, ColourByte.ByteEventEnum.UPDATEBACKGROUND), new ColourByte(0, ColourByte.ByteEventEnum.UPDATEBACKGROUND),
            };
            this.TextColour = TextColour ?? new ObservableCollection<ColourByte> { 
                new ColourByte(255, ColourByte.ByteEventEnum.UPDATETEXT), new ColourByte(255, ColourByte.ByteEventEnum.UPDATETEXT), new ColourByte(255, ColourByte.ByteEventEnum.UPDATETEXT) 
            };
            this.MainWindowHeight = MainWindowHeight;
            this.MainWindowWidth = MainWindowWidth;
            Inst = this;
        }
        public static AppSettings Inst
        {
            get => _inst;
            set => _inst = value;
        }
        static AppSettings _inst;





        /* MANAGER SETTINGS */

        public string GamePath { get => ManagerInstallation.GameFolder;  set => ManagerInstallation.GameFolder = value; }

        public static bool StaticHasShownOOBE { get => Inst.HasShownOOBE; set => Inst.HasShownOOBE = value; }
        public bool HasShownOOBE { get => _hasShownOOBE; set => _hasShownOOBE = value; }
        private bool _hasShownOOBE = false;

        // Background Colour
        public static event EventHandler StaticBackgroundColourChanged;
        public static void OnStaticBackgroundColourChanged()
        {
            StaticBackgroundColourChanged?.Invoke(null, EventArgs.Empty);
        }
        public static ObservableCollection<ColourByte> StaticBackgroundColour { get { return Inst.BackgroundColour; } set { Inst.BackgroundColour = value; OnStaticBackgroundColourChanged(); } }
        public ObservableCollection<ColourByte> BackgroundColour { get { return _backgroundColour; } 
            set { _backgroundColour = value; Trace.WriteLine("Setting (initial) Background Colour to " + string.Join(" ", value.Select(x => x.Byte))); } }
        private static ObservableCollection<ColourByte> _backgroundColour;

        // Main Window Size
        public static event EventHandler StaticWindowWidthChanged;
        
        public static double StaticMainWindowWidth { get => Inst.MainWindowWidth; set { Inst.MainWindowWidth = value; StaticWindowWidthChanged?.Invoke(null, EventArgs.Empty); } }
        public double MainWindowWidth { get => _mainWindowWidth; set { _mainWindowWidth = value;  } }
        private double _mainWindowWidth = 400;

        public static event EventHandler StaticWindowHeightChanged;
        public static double StaticMainWindowHeight { get => Inst.MainWindowHeight; set { Inst.MainWindowHeight = value; StaticWindowHeightChanged?.Invoke(null, EventArgs.Empty); } }
        public double MainWindowHeight { get => _mainWindowHeight; set { _mainWindowHeight = value; } }
        private double _mainWindowHeight = 400;

        public static double DefaultMainWindowHeight = 600;
        public static double DefaultMainWindowWidth = 1000;

        // Font Size
        public static event EventHandler StaticFontSizeMultiChanged;
        public static double StaticFontSizeMulti { get { return (double)(Inst?.FontSizeMulti ?? 1); } set { Inst.FontSizeMulti = value; } }
        public double FontSizeMulti
        {
            get { return _FontSizeMulti; }
            set
            {
                value = Math.Max(Math.Min(value, 4d), 1);
                _FontSizeMulti = value;
                StaticFontSizeMultiChanged?.Invoke(null, EventArgs.Empty); Trace.WriteLine("Setting FontSizeMulti to " + value.ToString());
            }
        }
        private static double _FontSizeMulti = 1.25; // 1.25 default

        

        // Font Colour
        public static event EventHandler StaticTextColourChanged;
        public static void OnStaticTextColourChanged()
        {
            StaticTextColourChanged?.Invoke(null, EventArgs.Empty);
        }
        public static ObservableCollection<ColourByte> StaticTextColour { get { return Inst.TextColour; } set { Inst.TextColour = value; OnStaticTextColourChanged(); } }
        public ObservableCollection<ColourByte> TextColour { get { return _textColour; } 
            set { _textColour = value; Trace.WriteLine("Setting (initial) Text Colour to " + string.Join(" ", value.Select(x => x.Byte))); } }
        private static ObservableCollection<ColourByte> _textColour;

    }
    public class ColourByte : INotifyPropertyChanged
    {
        public enum ByteEventEnum
        {
            UPDATEBACKGROUND = 1,
            UPDATETEXT = 2
        }
        public ByteEventEnum ByteEvent { get; set; }

        [JsonConstructor]
        public ColourByte(byte Byte, ByteEventEnum ByteEvent)
        {
            this.Byte = Byte;
            this.ByteEvent = ByteEvent;
        }
        
        public ColourByte(int Byte)
        { this.Byte = (byte)Byte; }

        public override string ToString()
        { return Byte.ToString(); }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public byte Byte { get { return _byte; } set { 
                _byte = value; OnPropertyChanged(); 
                if (ByteEvent == ByteEventEnum.UPDATEBACKGROUND) AppSettings.OnStaticBackgroundColourChanged();
                if (ByteEvent == ByteEventEnum.UPDATETEXT) AppSettings.OnStaticTextColourChanged();
            } }
        private byte _byte;
        public static implicit operator ColourByte(int i) => new ColourByte(i);
    }
}
