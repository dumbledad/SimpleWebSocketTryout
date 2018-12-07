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

        private void Connection_WebSocketDisconnected(WebSocketConnection sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private async void Connection_DataReceivedEvent(WebSocketConnection sender, SimpleWebSocketServer.DataReceivedEventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CommunicationTextBlock.Text = e.Data;
            });
            Debug.WriteLine($"From client: {e.Data}");
        }

        private async void Sender_Disconnected(WebSocketConnection sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CommunicationTextBlock.Text = "Disconnected";
            });
        }
    }
}
