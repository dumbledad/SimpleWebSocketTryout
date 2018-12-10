using SimpleWebSocketServer;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ContainerApp
{
    public sealed partial class MainPage : Page
    {
        private WebSocketServer server;
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (server == null)
            {
                server = new WebSocketServer(23949);
                server.ClientConnected += Server_ClientConnected1;
                server.Start();
                StartServerButton.IsEnabled = false;
            }
        }

        private async void Server_ClientConnected1(WebSocketServer sender, ClientConnectedEventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CommunicationTextBlock.Text = "Connected";
            });
            e.Connection.DataReceivedEvent += Connection_DataReceivedEvent;
            e.Connection.WebSocketDisconnected += Connection_WebSocketDisconnected;
        }

        private async void Connection_DataReceivedEvent(WebSocketConnection sender, SimpleWebSocketServer.DataReceivedEventArgs e)
        {
            var unmaskedApplicationData = e.UnmaskedApplicationData;
            var unmaskedApplicationDataString = Encoding.UTF8.GetString(unmaskedApplicationData.ToArray(), 0, unmaskedApplicationData.Count);
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CommunicationTextBlock.Text = unmaskedApplicationDataString;
            });
            Debug.WriteLine($"From client: {unmaskedApplicationDataString}");
        }

        private async void Connection_WebSocketDisconnected(WebSocketConnection sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CommunicationTextBlock.Text = "Disconnected";
            });
        }
    }
}
