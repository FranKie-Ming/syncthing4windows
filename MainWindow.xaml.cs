using System.Windows;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Awesomium.Core;
using System.Reflection;
using System.IO;
using System.Drawing;
using System.Web.Helpers;
using System.Collections.Generic;


namespace Syncthing4Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {        
        private App app;
        private EventLooper eventMonitor;
        private System.Windows.Forms.NotifyIcon tray = new System.Windows.Forms.NotifyIcon();
        private Dictionary<string, Icon> icons = new Dictionary<string, Icon>();

        private ObservableHashSet<string> iconStack = new ObservableHashSet<string>();
        private Dictionary<string, int> iconPriorities = new Dictionary<string, int>{
            {"bad", 7},
            {"pause", 6},
            {"alert", 5},
            {"flag", 4},
            {"down", 3},
            {"scan", 2},
            {"ok", 1},
        };

        public MainWindow()
        {
            app = ((App)Application.Current);
            InitializeComponent();
            this.Loaded += (object sender, RoutedEventArgs e) => SynchronizeSyncthing();
            this.Browser.LoginRequest += Browser_LoginRequest;
            this.Browser.LoadingFrameComplete += Browser_LoadingFrameComplete;

            LoadIcons();

            tray.Icon = icons["logo"];
            tray.Visible = true;
            tray.DoubleClick += tray_DoubleClick;

            iconStack.CollectionChanged += (s, e) => updateTrayIcon();
        }

        private void LoadIcons()
        {
            foreach (string name in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(name => name.EndsWith(".ico"))) 
            {
                icons.Add(
                    name.Split('.').Reverse().Skip(1).Take(1).First(),
                    new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                );
            }
        }

        void tray_DoubleClick(object sender, EventArgs e)
        {
            this.ShowInTaskbar = true;
            this.Show();
            this.WindowState = System.Windows.WindowState.Normal;
            this.Activate();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                this.Hide();
            }

            base.OnStateChanged(e);
        }

        async void Browser_LoadingFrameComplete(object sender, FrameEventArgs e)
        {
            Stream wrapper = Assembly.GetExecutingAssembly().GetManifestResourceStream("Syncthing4Windows.Resources.wrapper.js");
            if (wrapper != null)
            {
                this.Browser.ExecuteJavascript(new StreamReader(wrapper).ReadToEnd());
                ShowBrowser();
            }
            else
            {
                await this.ShowMessageAsync("Oops", "Can't load wrapper script. Exiting...", MessageDialogStyle.Affirmative);
                app.Shutdown();
            }
            
        }

        void Browser_LoginRequest(object sender, LoginRequestEventArgs e)
        {
            e.Username = app.GUIUsername;
            e.Password = app.GUIPassword;
            e.Handled = EventHandling.Modal;
            e.Cancel = false;
        }

        async private void SynchronizeSyncthing()
        {
            if (Process.GetProcessesByName("syncthing").Length > 0)
            {
                var result = await this.ShowMessageAsync("Oops", "Syncthing seems to be already running.\nWould you like to kill the existing instance to continue?", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
                {
                    AffirmativeButtonText = "Kill",
                    NegativeButtonText = "Exit"
                });

                if (result == MessageDialogResult.Affirmative)
                {
                    var progressCtrl = await this.ShowProgressAsync("Bare with me", "Killing existing syncthing processes");
                    Task.Factory.StartNew(() => KillSyncthing()).ContinueWith(async (task) =>
                    {
                        await progressCtrl.CloseAsync();
                        if (!task.Result)
                        {
                            await this.ShowMessageAsync("Oops", "Can't kill existing Syncthing processes, please do it via Task Manager, and try again.", MessageDialogStyle.Affirmative);
                            app.Shutdown();
                        }
                        else
                        {
                            StartSyncthing();
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                else
                {
                    app.Shutdown();
                }
            }
            else
            {
                StartSyncthing();
            }
        }

        private bool KillSyncthing()
        {
            foreach (int attempt in Enumerable.Range(1, 4))
            {
                if (attempt == 4)
                {
                    return false;
                }

                var processes = Process.GetProcessesByName("syncthing");

                if (processes.Length == 0)
                {
                    break;
                }

                foreach (Process proc in processes)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                        proc.Refresh();
                    }
                    catch { }
                }
            }
            return true;
        }

        private void StartSyncthing()
        {
            var handle = app.StartSyncthing();
            if (handle != null)
            {
                handle.OutputDataReceived += OutputReceived;
                handle.ErrorDataReceived += OutputReceived;

                handle.BeginOutputReadLine();
                handle.BeginErrorReadLine();

                eventMonitor = new EventLooper(app.GUIAddress, app.GUIAPIKey);
                registerMonitorCallbacks();
                this.Browser.Source = new Uri(app.GUIAddress);
            }            
        }

        void OutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                try
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.Log.Text += e.Data + "\n";
                        this.Log.ScrollToEnd();
                    });
                }
                catch (TaskCanceledException)
                {                    
                }
            }
        }

        void Browser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            ShowBrowser();
        }

        private void ShowBrowser()
        {
            ShowBrowser(null, null);
        }

        private void ShowBrowser(object sender, RoutedEventArgs e)
        {
            this.LogGrid.Visibility = Visibility.Hidden;
            this.SpinnerGrid.Visibility = Visibility.Hidden;
            this.BrowserGrid.Visibility = Visibility.Visible;

            this.LogButton.Visibility = Visibility.Visible;
            this.BrowserButton.Visibility = Visibility.Collapsed;
        }

        private void ShowLog()
        {
            ShowLog(null, null);
        }

        private void ShowLog(object sender, RoutedEventArgs e)
        {
            this.LogGrid.Visibility = Visibility.Visible;
            this.SpinnerGrid.Visibility = Visibility.Hidden;
            this.BrowserGrid.Visibility = Visibility.Hidden;

            this.LogButton.Visibility = Visibility.Collapsed;
            this.BrowserButton.Visibility = Visibility.Visible;
        }

        private void ShowSpinner()
        {
            ShowSpinner(null, null);
        }

        private void ShowSpinner(object sender, RoutedEventArgs e)
        {
            this.LogGrid.Visibility = Visibility.Hidden;
            this.SpinnerGrid.Visibility = Visibility.Visible;
            this.BrowserGrid.Visibility = Visibility.Hidden;

            this.LogButton.Visibility = Visibility.Visible;
            this.BrowserButton.Visibility = Visibility.Collapsed;
        }

        private void CogClick(object sender, RoutedEventArgs e)
        {
            this.Settings.IsOpen = !this.Settings.IsOpen;
        }

        private void BrowserRefresh(object sender, RoutedEventArgs e)
        {
            ShowSpinner();
            this.Browser.Reload(true);
        }

        private void registerMonitorCallbacks()
        {
            eventMonitor.On("UIOnline", (evnt) =>
            {
                if (this.BrowserGrid.Visibility != Visibility.Visible)
                {
                    ShowBrowser();
                }
            });

            eventMonitor.On("UIOffline", (evnt) =>
            {
                if (this.SpinnerGrid.Visibility != Visibility.Visible)
                {
                    ShowSpinner();
                }
            });

            eventMonitor.On("Traffic", (evnt) =>
            {
                iconStack.Add("down");
            });

            eventMonitor.On("Traffic", (evnt) =>
            {
                iconStack.Remove("icon");
            });


#if DEBUG
            eventMonitor.OnAll((type, evnt) =>
            {
                string data = "";
                if (evnt != null)
                {
                    data = Json.Encode(evnt);
                }

                this.Log.Text += "Event type: " + type + " " + data + "\n";
                this.Log.ScrollToEnd();
            });
#endif
        }

        private void updateTrayIcon()
        {
            var max = 0;
            var maxIcon = "logo";

            foreach (string icon in iconStack)
            {
                var priority = iconPriorities[icon];
                if (priority > max) {
                    max = priority;
                    maxIcon = icon;
                }
            }
            tray.Icon = icons[maxIcon];
        }
    }
}
