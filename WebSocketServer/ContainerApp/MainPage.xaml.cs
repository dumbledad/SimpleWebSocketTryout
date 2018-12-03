using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using WebSocketServerStandard;
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
                server.Logger = new DebugTextWriter(); //Console.Out;
                server.LogLevel = ServerLogLevel.Subtle;
                server.ClientConnected += Server_ClientConnected;
                server.Start();
                StartServerButton.IsEnabled = false;
            }
        }

        private async void Server_ClientConnected(WebSocketConnection sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CommunicationTextBlock.Text = "Connected";
            });
            sender.Disconnected += Sender_Disconnected;
            sender.DataReceived += Sender_DataReceived;
        }

        private async void Sender_DataReceived(WebSocketConnection sender, WebSocketServerStandard.DataReceivedEventArgs e)
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
            server = null;
            StartServerButton.IsEnabled = false;
        }
    }

    // https://stackoverflow.com/a/4234085/575530
    public class DebugTextWriter : StreamWriter
    {
        public DebugTextWriter() : base(new DebugOutStream(), Encoding.Unicode, 1024)
        {
            this.AutoFlush = true;
        }

        public override Encoding Encoding => throw new NotImplementedException();

        class DebugOutStream : Stream
        {
            public override void Write(byte[] buffer, int offset, int count)
            {
                Debug.Write(Encoding.Unicode.GetString(buffer, offset, count));
            }

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override void Flush() { Debug.Flush(); }
            public override long Length { get { throw new InvalidOperationException(); } }
            public override int Read(byte[] buffer, int offset, int count) { throw new InvalidOperationException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new InvalidOperationException(); }
            public override void SetLength(long value) { throw new InvalidOperationException(); }
            public override long Position
            {
                get { throw new InvalidOperationException(); }
                set { throw new InvalidOperationException(); }
            }
        };
    }
}
