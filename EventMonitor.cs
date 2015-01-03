using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;
using System.Windows.Threading;

namespace Syncthing4Windows
{
    class Request : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = 2 * 60 * 1000;
            return w;
        }
    }

    class EventLooper
    {
        private int lastId = 0;
        private bool online = false;
        private string apikey;
        private string address;
        private Dictionary<String, List<Action<dynamic>>> callbacks = new Dictionary<string,List<Action<dynamic>>>();

        private DateTime lastTrafficUpdate;
        private dynamic lastTraffic;

        public EventLooper(string address, string apikey)
        {
            this.address = address;
            this.apikey = apikey;
            
            Delay(FireEventRequest, 1000);
            Delay(FireErrorRequest, 1000);
            Delay(FireConnectionRequest, 1000);
        }

        private void FireRequest(string url, Action<object, DownloadDataCompletedEventArgs> callback)
        {
            var request = new Request();
            request.Headers.Add("X-API-Key: " + apikey);
            request.DownloadDataCompleted += new DownloadDataCompletedEventHandler(callback);
            request.DownloadDataAsync(new Uri(address + url));
        }

        void OnEventResponse(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null && !e.Cancelled)
            {
                Online();

                dynamic events = Json.Decode(Encoding.Default.GetString(e.Result));
                foreach (dynamic evnt in events)
                {
                    lastId = Math.Max(lastId, evnt.id);
                    
                    Emit(evnt.type, evnt);
                }
                Delay(FireEventRequest, 500);
            }
            else
            {
                Offline();
                Delay(FireLimitedEventRequest, 2000);
            }
             
        }

        void Online()
        {
            if (!online)
            {
                online = true;
                Emit("UIOnline", null);
            }
        }


        void Offline()
        {
            if (online)
            {
                online = false;
                Emit("UIOffline", null);
            }
        }

        void Emit(string type, dynamic evnt) 
        {
            List<Action<dynamic>> eventCallbacks;
            if (callbacks.TryGetValue(type.ToLower(), out eventCallbacks))
            {
                foreach (Action<dynamic> action in eventCallbacks)
                {
                    try
                    {
                        action.Invoke(evnt);
                    }
                    catch { }
                }
            }
#if DEBUG
            foreach (Action<string, dynamic> action in allCallbacks)
            {
                try
                {
                    action.Invoke(type, evnt);
                }
                catch { }
            }
        }

        private List<Action<string, dynamic>> allCallbacks = new List<Action<string, dynamic>>();

        public void OnAll(Action<string, dynamic> action)
        {
            allCallbacks.Add(action);
#endif
        }

        void FireEventRequest()
        {
            FireRequest("/rest/events?since=" + lastId, OnEventResponse);
        }

        void FireLimitedEventRequest()
        {
            lastId = 0;
            FireRequest("/rest/events?limit=1", OnEventResponse);
        }

        private void FireErrorRequest()
        {
            FireRequest("/rest/errors", (s, e) =>
            {
                if (e.Error == null && !e.Cancelled)
                {
                    Online();
                    dynamic errors = Json.Decode(Encoding.Default.GetString(e.Result));
                    if (errors.Length > 0)
                    {
                        Emit("Error", errors);
                    }
                    else
                    {
                        Emit("ErrorClear", null);
                    }
                } else {
                    Offline();
                }
                Delay(FireErrorRequest, 2000);
            });
        }

        private void FireConnectionRequest()
        {
            FireRequest("/rest/connections", (s, e) =>
            {
                if (e.Error == null && !e.Cancelled)
                {
                    Online();
                    dynamic stats = Json.Decode(Encoding.Default.GetString(e.Result));
                    if (lastTraffic != null)
                    {                    
                        if (stats.total.InBytesTotal > lastTraffic.InBytesTotal || stats.total.OutBytesTotal > lastTraffic.OutByteoutBytes) 
                        {
                            dynamic bps = new Object();
                            var diff = DateTime.Now - lastTrafficUpdate;
                            bps.InBps = (stats.total.InBytesTotal - lastTraffic.InBytesTotal) / diff.TotalSeconds;
                            bps.OutBps = (stats.total.OutBytesTotal - lastTraffic.OutBytesTotal) / diff.TotalSeconds;
                            Emit("Traffic", bps);
                        }
                        else
                        {
                            Emit("NoTraffic", null);
                        }
                    }
                    lastTrafficUpdate = DateTime.Now;
                    lastTraffic = stats.total;
                }
                else
                {
                    Offline();
                    
                }
                Delay(FireConnectionRequest, 2000);
            });
        }

        void Delay(Action fn, int delay)
        {
            var currentDispatcher = Dispatcher.CurrentDispatcher;
            new Task(() =>
            {
                System.Threading.Thread.Sleep(delay);
                currentDispatcher.BeginInvoke(fn);
            }).Start();
        }

        public void On(string name, Action<dynamic> action) {
            name = name.ToLower();
            if (!callbacks.ContainsKey(name))
            {
                callbacks.Add(name, new List<Action<dynamic>>());
            }
            callbacks[name].Add(action);
        }
    }
}
