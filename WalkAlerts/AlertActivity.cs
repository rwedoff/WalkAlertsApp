using System;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using Android.Views.Accessibility;
using Android.Media;
using Android.Content.Res;
using Android.Graphics;
using System.Threading;

namespace WalkAlerts
{
    [Activity(Label = "Alerts", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class AlertActivity : Activity
    {
        // The port number for the remote device.
        private static int port;
        private static IPAddress ipAddress;
        private static Socket sender;
        private static Vibrator vibrator;
        private AccessibilityManager accessibilityManager;
        private MediaPlayer prohibMediaPlayer;
        private LinearLayout mainView;
        private TextView alertText;
        private MediaPlayer pervMediaPlayer;
        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);
        // The response from the remote device.  
        private static String response = String.Empty;


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create your application here
            SetContentView(Resource.Layout.AlertLayout);
            accessibilityManager = (AccessibilityManager)GetSystemService(AccessibilityService);

            View decorView = Window.DecorView;
            var uiOptions = (int)decorView.SystemUiVisibility;
            var newUiOptions = uiOptions;

            Window.SetFlags(WindowManagerFlags.KeepScreenOn, WindowManagerFlags.KeepScreenOn);

            //newUiOptions |= (int)SystemUiFlags.Fullscreen;
            newUiOptions |= (int)SystemUiFlags.HideNavigation;
            newUiOptions |= (int)SystemUiFlags.Immersive;

            decorView.SystemUiVisibility = (StatusBarVisibility)newUiOptions;

            if (!IPAddress.TryParse(Intent.Extras.GetString("ipAddress"), out ipAddress))
            {
                Console.WriteLine("ERROR in IP");
                Toast.MakeText(ApplicationContext, "Not Connected. Error in IP Address", ToastLength.Long).Show();
            }
            if(!int.TryParse(Intent.Extras.GetString("port"), out port))
            {
                Toast.MakeText(ApplicationContext, "Not Connected. Error in Port", ToastLength.Long).Show();
                Console.WriteLine("ERROR in port");
            }

            vibrator = (Vibrator)GetSystemService(VibratorService);

            mainView = FindViewById<LinearLayout>(Resource.Id.clickerView);
            alertText = FindViewById<TextView>(Resource.Id.alertText);
            var reconnectButton = FindViewById<Button>(Resource.Id.reConnectButton);
            reconnectButton.Click += ReConnect;

            //Load the Prohibitive Sound Assets
            try
            {
                AssetFileDescriptor afd = Assets.OpenFd("prohibSound.wav");
                prohibMediaPlayer = new MediaPlayer();
                prohibMediaPlayer.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                prohibMediaPlayer.Prepare();
            }
            catch(Exception e) {
                Console.WriteLine("Prohibitive Alert sound file error: " + e);
            };
            try
            {
                AssetFileDescriptor afd = Assets.OpenFd("pervSound.wav");
                pervMediaPlayer = new MediaPlayer();
                pervMediaPlayer.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                pervMediaPlayer.Prepare();
            }
            catch (Exception e)
            {
                Console.WriteLine("Prohibitive Alert sound file error: " + e);
            };
        }

        /// <summary>
        /// Reconnects the socket on click handler
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void ReConnect(Object s, EventArgs e)
        {
            var task = new Task(() => StartClient());
            task.Start();
        }

        protected override void OnStart()
        {
            base.OnStart();
            if (sender == null || !sender.Connected)
            {
                var task = new Task(() => StartClient());
                task.Start();
            }
        }

        protected override void OnStop()
        {
            base.OnStop();
            if (sender != null)
            {
                if (sender.Connected)
                {
                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }
            }
        }

        /// <summary>
        /// Starts the TCP Client on a new thread. Loops receiving message code.
        /// </summary>
        public void StartClient()
        {
            // Data buffer for incoming data.
            byte[] bytes = new byte[1024];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.  
            sender = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            // Connect to the remote endpoint.  
            sender.BeginConnect(remoteEP,
                new AsyncCallback(ConnectCallback), sender);
            connectDone.WaitOne();

            //// Send test data to the remote device.  
            //Send(client, "This is a test<EOF>");
            //sendDone.WaitOne();

            // Receive the response from the remote device.  
            Receive(sender);
            while (true)
            {
                if (!SocketExtensions.IsConnected(sender))
                {
                    using (var h = new Handler(Looper.MainLooper))
                    {
                        h.Post(() => {
                            var view = FindViewById<LinearLayout>(Resource.Id.connectView);
                            view.Visibility = ViewStates.Visible;
                        });
                    }
                    receiveDone.Set();
                    break;
                }
                Thread.Sleep(1000);
            }

        }

        /// <summary>
        /// Async callback for TCP message
        /// </summary>
        /// <param name="ar"></param>
        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}",
                    client.RemoteEndPoint.ToString());
                using (var h = new Handler(Looper.MainLooper))
                {
                    h.Post(() => {
                        var view = FindViewById<LinearLayout>(Resource.Id.connectView);
                        view.Visibility = ViewStates.Gone;
                        Toast.MakeText(Application.Context, "Connected", ToastLength.Long).Show();
                    });
                }
                // Signal that the connection has been made.  
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Receiaves the TCP socket message
        /// </summary>
        /// <param name="client"></param>
        private void Receive(Socket client)
        {
            try
            {
                // Create the state object.  
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.  
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Async receive callback for TCP message
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket   
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.  
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.  
                    string mess = System.Text.Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                    state.sb.Append(mess);

                    // Get the rest of the data.  
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                    ProcessMessage(mess);
                }
                else
                {
                    // All the data has arrived; put it in response.  
                    if (state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                    }
                    // Signal that all bytes have been received.  
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Send method for sending a TCP message
        /// </summary>
        /// <param name="client"></param>
        /// <param name="data"></param>
        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = System.Text.Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        /// <summary>
        /// Callback for sending a message
        /// </summary>
        /// <param name="ar"></param>
        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.  
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Processes a received message and splits into Prohibitive
        /// or Permissive Alerts
        /// </summary>
        /// <param name="message"></param>
        private void ProcessMessage(string message)
        {
            if (message.Contains("PROHIBITIVEALERT;"))
            {
                ReceivedProhibAlert();
            }
            else if (message.Contains("PERMISSIVEALERT;"))
            {
                ReceivedPermAlert();
            }
        }

        /// <summary>
        /// When a Prohibitive Alert is received, this method handles the UI action
        /// </summary>
        private void ReceivedProhibAlert()
        {
            RunOnUiThread(async () =>
            {
                #pragma warning disable CS0618 // Type or member is obsolete
                vibrator.Vibrate(400); //Vibrate is deprecated, but VibrationEffect didn't work
                #pragma warning restore CS0618 // Type or member is obsolete
                prohibMediaPlayer.Start();
                mainView.SetBackgroundColor(Color.Red);
                alertText.Text = GetString(Resource.String.prohib_text);
                alertText.Visibility = ViewStates.Visible;
                alertText.TextAlignment = TextAlignment.Center;
                alertText.SetForegroundGravity(GravityFlags.Center);
                alertText.Gravity = GravityFlags.Center;
                alertText.SetTextColor(Color.White);
                await Task.Delay(4000);
                mainView.SetBackgroundColor(Color.Black);
                alertText.Visibility = ViewStates.Invisible;
            });
        }

        /// <summary>
        /// When a Permissive Alert is received, this method handles the UI action
        /// </summary>
        private void ReceivedPermAlert()
        {
            RunOnUiThread(async () =>
            {
                #pragma warning disable CS0618 // Type or member is obsolete
                vibrator.Vibrate(200); //Vibrate is deprecated, but VibrationEffect didn't work
                #pragma warning restore CS0618 // Type or member is obsolete
                //Play the sound
                pervMediaPlayer.Start();
                mainView.SetBackgroundColor(Color.Black);
                alertText.Text = GetString(Resource.String.perv_text);
                alertText.SetTextColor(Color.LightGreen);
                alertText.TextAlignment = TextAlignment.Center;
                alertText.SetForegroundGravity(GravityFlags.Center);
                alertText.Gravity = GravityFlags.Center;
                alertText.Visibility = ViewStates.Visible;
                //Reset everything back to normal
                await Task.Delay(4000);
                mainView.SetBackgroundColor(Color.Black);
                alertText.Visibility = ViewStates.Invisible;
            });
        }



        /// <summary>
        /// Sends string messages to connected Server
        /// </summary>
        /// <param name="message"></param>
        private void SendMessage(string message)
        {
            if (sender.Connected)
            {
                // Encode the data string into a byte array.
                byte[] msg = System.Text.Encoding.ASCII.GetBytes(message);

                // Send the data through the socket.
                int bytesSent = sender.Send(msg);
            }
        }

    }

    /// <summary>
    /// Static utility that checks if a socket is still connected or not.
    /// </summary>
    static class SocketExtensions
    {
        public static bool IsConnected(this Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
        }
    }


    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }

}