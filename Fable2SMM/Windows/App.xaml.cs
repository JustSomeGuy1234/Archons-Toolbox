using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Diagnostics;

namespace Fable2SMM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((object obj, UnhandledExceptionEventArgs args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                Trace.WriteLine(ex.Message + "\n");
                Trace.WriteLine(ex.InnerException + "\n");
                Trace.WriteLine(ex.StackTrace);
            });

            base.OnStartup(e);
            //new DebugListener();
        }
    }
}
