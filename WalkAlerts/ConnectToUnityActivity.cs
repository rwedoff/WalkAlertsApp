using System;
using System.Collections.Generic;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;
using Android.Support.V7.Widget;
using Android.Preferences;

namespace WalkAlerts
{
    [Activity(Label = "Connect")]
    public class ConnectToUnityActivity : Activity
    {
        private const int listenPort = 11000;
        private TextView statusText;
        private RecyclerView recyclerView;
        private ConnectListAdapter adapter;
        private static List<ComputerInfo> serverList = new List<ComputerInfo>();
        private readonly object lockList = new object();
        private bool messageReceived;
        private string ipString;
        private string udpString;
        private string portString;
        private EditText portEditText;
        private EditText ipEditText;
        private ISharedPreferences preferences;
        private EditText udpEditText;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.ConnectLayout);
            // Create your application here
            Button searchButton = FindViewById<Button>(Resource.Id.SearchButton);
            searchButton.Click += BeginSearchClick;

            statusText = FindViewById<TextView>(Resource.Id.statusText);

            preferences = PreferenceManager.GetDefaultSharedPreferences(this);
            ipEditText = FindViewById<EditText>(Resource.Id.ipEditText);
            ipEditText.Text = preferences.GetString("ipAddress", "0.0.0.0");

            portEditText = FindViewById<EditText>(Resource.Id.portEditText);
            portEditText.Text = preferences.GetString("port", "3000");

            udpEditText = FindViewById<EditText>(Resource.Id.udpEditText);
            udpEditText.Text = preferences.GetString("updAddress", "239.255.42.99");

            Button connectButton = FindViewById<Button>(Resource.Id.connectButton);
            connectButton.Click += ConnectToServer;

            recyclerView = FindViewById<RecyclerView>(Resource.Id.serverRecyclerView);

            // improve performance if you know that changes in content
            // do not change the size of the RecyclerView
            recyclerView.HasFixedSize = false;

            // use a linear layout manager
            LinearLayoutManager layoutManager = new LinearLayoutManager(this);
            recyclerView.SetLayoutManager(layoutManager);

            // specify an adapter
            adapter = new ConnectListAdapter()
            {
                items = serverList
            };

            adapter.ItemClick += OnItemClick;
            recyclerView.SetAdapter(adapter);
            //BeginSearch();
        }

        public void ConnectToServer(Object sender, EventArgs e)
        {
            if (ipEditText == null)
                return;

            if (portEditText == null)
                return;

            ipString = ipEditText.Text.ToString();
            String portStr = portEditText.Text.ToString();
            udpString = udpEditText.Text.ToString();

            IPAddress.TryParse(ipString, out IPAddress ip);
            IPAddress.TryParse(udpString, out IPAddress udpip);

            if (ip == null)
            {
                Toast.MakeText(Application.Context, "Inncorect IP Address Format. Try again...", ToastLength.Long).Show();
                return;
            }
            if (portStr == null)
            {
                Toast.MakeText(ApplicationContext, "Port not correct. Try again...", ToastLength.Short).Show();
                return;
            }
            if (udpip == null)
            {
                Toast.MakeText(ApplicationContext, "UDP IP Address not correct. Try again...", ToastLength.Short).Show();
                return;
            }

            ISharedPreferencesEditor preferencesEditor = preferences.Edit();
            preferencesEditor.PutString("ipAddress", ipString);
            preferencesEditor.PutString("port", portStr);
            preferencesEditor.PutString("udpAddress", udpString);
            preferencesEditor.Apply();

            Intent intent = new Intent(this, typeof(AlertActivity));

            intent.PutExtra("ipAddress", ipString);
            intent.PutExtra("port", portStr);
            StartActivity(intent);
        }

        void OnItemClick(object sender, string serverInfo)
        {
            var info = serverInfo.Split(';');
            ipString = info[0];
            portString = info[1];
            AutoConnectToServer();
        }

        protected override void OnStop()
        {
            base.OnStop();
        }

        public void AutoConnectToServer()
        {
            String udpStr = udpEditText.Text.ToString();
            IPAddress.TryParse(ipString, out IPAddress ip);
            IPAddress.TryParse(udpStr, out IPAddress udp);

            if (ip == null)
            {
                Toast.MakeText(Application.Context, "Inncorect IP Address Format. Try Again...", ToastLength.Long).Show();
                return;
            }

            if (udp == null)
            {
                Toast.MakeText(Application.Context, "Inncorect UDP IP Address Format. Try Again...", ToastLength.Long).Show();
                return;
            }

            if (portString == null)
            {
                Toast.MakeText(Application.Context, "Inncorect Port Format. Try Again...", ToastLength.Long).Show();
                return;
            }

            portEditText.Text = portString;
            ipEditText.Text = ipString;

            ISharedPreferencesEditor preferencesEditor = preferences.Edit();
            preferencesEditor.PutString("ipAddress", ipString);
            preferencesEditor.PutString("port", portString);
            preferencesEditor.PutString("udpAddress", udpStr);
            preferencesEditor.Apply();

            Intent intent = new Intent(this, typeof(AlertActivity));
            intent.PutExtra("ipAddress", ipString);
            intent.PutExtra("port", portString);
            StartActivity(intent);
        }

        #region Server Code
        private void BeginSearch()
        {
            udpString = udpEditText.Text.ToString();
            serverList.Clear();
            adapter.items = serverList;
            adapter.NotifyDataSetChanged();
            ThreadPool.QueueUserWorkItem(o => ReceiveMessages());
        }
        private void BeginSearchClick(Object sender, EventArgs e)
        {
            BeginSearch();
        }

        public void ReceiveCallback(IAsyncResult ar)
        {
            UdpClient u = ((UdpState)(ar.AsyncState)).u;
            IPEndPoint e = ((UdpState)(ar.AsyncState)).e;
            try
            {
                Byte[] receiveBytes = u.EndReceive(ar, ref e);
                string receiveString = Encoding.ASCII.GetString(receiveBytes);
                Console.WriteLine("Received: {0}", receiveString);
                
                lock (lockList)
                {
                    string[] receiveItems = receiveString.Split(';');
                    if (receiveItems.Length > 2)
                    {
                        if (serverList.FindIndex((elm) => elm.Name.Equals(receiveItems[1])) < 0)
                        {
                            serverList.Add(new ComputerInfo()
                            {
                                IpString = receiveItems[0],
                                Name = receiveItems[1],
                                PortStr = receiveItems[2]
                            });
                        }
                    }
                }

                }
            catch { }
            finally {
                messageReceived = true;
            }
            
        }

        public void ReceiveMessages()
        {
            messageReceived = false;
            if (!IPAddress.TryParse(udpString, out IPAddress udpIP))
            {
                using (var h = new Handler(Looper.MainLooper))
                {
                    h.Post(() => {
                        Toast.MakeText(Application.Context, "Inncorect UDP IP Address Format. Try Again...", ToastLength.Long).Show();
                    });
                }
                return;
            }
            // Receive a message and write it to the console.
            IPEndPoint endPoint = new IPEndPoint(udpIP, listenPort);
            UdpClient client = new UdpClient(endPoint);

            UdpState state = new UdpState
            {
                e = endPoint,
                u = client
            };

            Console.WriteLine("listening for messages");
            client.BeginReceive(new AsyncCallback(ReceiveCallback), state);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            RunOnUiThread(() =>
            {
                Console.WriteLine("Scanning...");
                statusText.Text = "Status: Scanning...";
                Toast.MakeText(this, "Status: Scanning...", ToastLength.Long).Show();
            });
            while(stopwatch.ElapsedMilliseconds < 7000)
            {
                Thread.Sleep(1000);
                Console.WriteLine("Trying...");
                if (messageReceived)
                {
                    messageReceived = false;
                    client.BeginReceive(new AsyncCallback(ReceiveCallback), state);
                }
            }
            client.Close();
            stopwatch.Stop();
            Console.WriteLine("Status: Done Searching");
            RunOnUiThread(() =>
            {
                if (serverList.Count == 0)
                {
                    statusText.Text = "No computers found. Try Again or try manual entry.";
                    Toast.MakeText(this, "No computers found. Try Again or try manual entry", ToastLength.Short).Show();
                }
                else
                {
                    statusText.Text = "Status: Done Searching";
                    Toast.MakeText(this, "Done searching! Computers found!", ToastLength.Short).Show();
                    adapter.items = serverList;
                    adapter.NotifyDataSetChanged();
                }
            });
        }

        class UdpState
        {
            public IPEndPoint e;
            public UdpClient u;
        }
#endregion

    }
}