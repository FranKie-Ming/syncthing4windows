using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Syncthing4Windows
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Process ProcessHandle;

        public string GUIAddress { get; private set; }
        public string GUIAPIKey { get; private set; }
        public string GUIUsername { get; private set; }
        public string GUIPassword { get; private set; }

        private void ApplicationExit(object sender, ExitEventArgs e)
        {
            if (ProcessHandle != null && !ProcessHandle.HasExited)
            {
                try
                {
                    ProcessHandle.Kill();
                    ProcessHandle.WaitForExit();
                }
                catch { }
            }
        }

        public Process StartSyncthing()
        {
            if (ProcessHandle != null)
            {
                if (ProcessHandle.HasExited)
                {
                    var handles = Process.GetProcessesByName("syncthing");
                    if (handles.Length > 0)
                    {
                        ProcessHandle = handles[0];
                    }
                }
                if (!ProcessHandle.HasExited)
                    return null;
            }

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syncthing.exe");
            if (!File.Exists(path))
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Syncthing4Windows.Resources.syncthing.exe"))
                {
                    using (FileStream fileStream = new FileStream(path, FileMode.Create))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
        
            GUIAddress = "http://127.0.0.1:8080";
            GUIAPIKey = Guid.NewGuid().ToString();
            GUIUsername = Guid.NewGuid().ToString();
            GUIPassword = Guid.NewGuid().ToString();

            ProcessStartInfo ProcessInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            ProcessInfo.EnvironmentVariables.Add("STGUIADDRESS", GUIAddress);
            ProcessInfo.EnvironmentVariables.Add("STGUIAPIKEY", GUIAPIKey);
            //ProcessInfo.EnvironmentVariables.Add("STGUIAUTH", GUIUsername + ":" + GUIPassword);
            ProcessInfo.EnvironmentVariables.Add("STGUIAUTH", ":" + GUIPassword);
            
            ProcessHandle = Process.Start(ProcessInfo);

            return ProcessHandle;
        }
    }
}
