using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot
{
    class RemoteControl
    {
        private class DataState
        {
            public byte[] buffer = new byte[1024];
            public Socket socket;
            public StringBuilder sb = new StringBuilder();
        }

        private TcpClient _client = new TcpClient();
        private ASCIIEncoding asen = new ASCIIEncoding();
        private ManualResetEvent _mre = new ManualResetEvent(false);

        public void Start()
        {
            try
            {
                var ip = "192.168.0.194";
                if (Environment.MachineName != "PIPE-PC")
                    ip = "77.230.213.175";

                _client = new TcpClient();
                _client.BeginConnect(ip, 8001, ConnectCallback, null);

                Task.Run(() => {
                    _mre.WaitOne();
                    while (IsConnected())
                    {
                        Thread.Sleep(2000);
                    }
                    _mre.Reset();
                    _client.Close();
                    Start();
                });
            }

            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.Message);
            }
        }

        private bool IsConnected()
        {
            try
            {
                _client.Client.Send(new byte[0]);
            }
            catch (SocketException)
            {
                return false;
            }
            return true;
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            if (_client == null)
                return;
            try
            {
                _client.EndConnect(ar);
                _mre.Set();

                var s = _client.Client;

                Receive(s);
            }
            catch (SocketException)
            {
                Task.Run(() => {
                    Thread.Sleep(2000);
                    Start();
                });
            }
            catch (ArgumentException) { }

        }

        public void Close()
        {
            _client.Close();
        }

        public void Send(string str)
        {
            if (!_client.Connected) return;

            var data = asen.GetBytes(str);
            _client.Client.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, _client.Client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            Socket client = (Socket)ar.AsyncState;
            client.EndSend(ar);
        }

        private void Receive(Socket s)
        {
            var state = new DataState { socket = s };
            s.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, ReceiveCallback, state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var state = (DataState)ar.AsyncState;
            var s = state.socket;

            int bytesRead = -1;
            try
            {
                bytesRead = s.EndReceive(ar);
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            if (bytesRead > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                Program.Instance.ConsoleCmd(state.sb.ToString());
            }

            Receive(s);
        }
    }
}
