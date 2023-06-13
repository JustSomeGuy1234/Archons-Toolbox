using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Fable2SMM
{
    public class DebugListener : TraceListener
    {
        public static string ListenerLog { get => _listenerLog.ToString(); set { _listenerLog.Clear().Append(value); ListenerLogChanged?.Invoke(null, EventArgs.Empty); } }
        private static StringBuilder _listenerLog = new StringBuilder();
        public static event EventHandler ListenerLogChanged;

        private static StreamWriter FileWriter;
        private bool FileWriterAvailable;

        public DebugListener()
        {
            if (FileWriter == null)
            {
                try 
                {
                    FileWriter = new StreamWriter("./Manager.log", false);
                    FileWriterAvailable = true;
                } catch (Exception e)
                {
                    MessageBox.Show("Failed to open Manager.log file!\nMake sure you haven't placed the Manager in a protected folder like ProgramFiles.\n\n" + e.Message);
                    FileWriterAvailable = false;
                }
            }
        }

        public override void Write(string message)
        {
            if (FileWriterAvailable)
            {
                FileWriter.Write(message);
                FileWriter.Flush();
            }

            _listenerLog.Append(message);
            ListenerLogChanged?.Invoke(null, EventArgs.Empty);
        }
        public override void Write(object o)
        {
            Write((o ?? "null").ToString());
        }
        public override void WriteLine(string message)
        {
            Write("\n" + (message ?? "null") + "\n");
        }
        public override void WriteLine(object o)
        {
            if (o == null)
                WriteLine("null");
            else
                WriteLine(o.ToString());
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            WriteLine("---------- ERROR ----------\n\n" +
                message +
                      "\n\n-----------------------------");
        }
    }
}
