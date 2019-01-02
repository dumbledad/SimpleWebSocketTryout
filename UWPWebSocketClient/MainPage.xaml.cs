using System;
using System.Diagnostics;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web;

namespace UWPWebSocketClient
{
    // https://docs.microsoft.com/en-us/windows/uwp/networking/websockets
    public sealed partial class MainPage : Page
    {
        private MessageWebSocket Socket = new MessageWebSocket();

        public MainPage()
        {
            this.InitializeComponent();

            ApplicationView.PreferredLaunchViewSize = new Size(400, 200);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            Socket.Control.MessageType = SocketMessageType.Utf8;

            Socket.MessageReceived += Socket_MessageReceived;
            Socket.Closed += Socket_Closed;

            try
            {
                //await Socket.ConnectAsync(new Uri("ws://192.168.43.112:23949"));
                await Socket.ConnectAsync(new Uri("ws://10.164.184.151:23949"));
                Debug.WriteLine("websocket is connected");
            }
            catch (Exception ex)
            {
                WebErrorStatus webErrorStatus = WebSocketError.GetStatus(ex.GetBaseException().HResult);
                Debug.WriteLine(ex.Message);
                Debug.WriteLine($"websocket error status: {webErrorStatus}");
            }
        }

        private async void SayHalloButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dataWriter = new DataWriter(Socket.OutputStream))
            {
                dataWriter.WriteString("Hallo Server");
                await dataWriter.StoreAsync();
                dataWriter.DetachStream();
            }
            Debug.WriteLine("Sending message using websocket");
        }

        private void Socket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            Debug.WriteLine($"got some text: {args.ToString()}");
        }

        private void Socket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Debug.WriteLine($"websocket is disconnected: {args.Reason}");
        }
    }
}
