using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteController
{
    class Program
    {
        private class DataState
        {
            public byte[] buffer = new byte[1024];
            public Socket socket;
            public StringBuilder sb = new StringBuilder();
        }


        static void Main(string[] args)
        {
            while (!new Program().Start()) Console.Clear();
        }

        private bool _exit;

        bool Start()
        {
            try
            {
                IPAddress ipAd = IPAddress.Parse("192.168.0.194");
                TcpListener myList = new TcpListener(ipAd, 8001);

                myList.Start();

                Console.WriteLine("The server is running at port 8001...");
                Console.WriteLine("The local End point is " + myList.LocalEndpoint);
                Console.WriteLine("Waiting for a connection.....");

                Socket s = myList.AcceptSocket();
                Console.WriteLine("Connection accepted from " + s.RemoteEndPoint);

                Receive(s);

                ASCIIEncoding asen = new ASCIIEncoding();
                while (!_exit)
                {
                    string input = Console.ReadLine() ?? "";
                    if (input == "!exit")
                        return true;
                    s.Send(asen.GetBytes(input));
                    //ConsoleEx.WriteLineColor(ConsoleColor.DarkGreen, input);
                }

                /* clean up */
                s.Close();
                myList.Stop();

            }
            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
            }
            return false;
        }

        private void Receive(Socket s)
        {
            try
            {
                var state = new DataState { socket = s };
                s.BeginReceive(state.buffer, 0, state.buffer.Length, SocketFlags.None, ReceiveCallback, state);
            }
            catch (SocketException)
            {
                _exit = true;
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var state = (DataState) ar.AsyncState;
            var s = state.socket;

            int bytesRead = -1;

            try
            {
                bytesRead = s.EndReceive(ar);
            }
            catch (Exception)
            {
                return;
            }

            if (bytesRead > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                string rec = state.sb.ToString();

                for (int i = 0; i < rec.Length; i++)
                {
                    char c = rec[i];
                    if (c == '@')
                    {
                        Console.ForegroundColor = (ConsoleColor) int.Parse(rec[i + 1].ToString(), System.Globalization.NumberStyles.HexNumber);
                        i++;
                    }
                    else
                    {
                        Console.Write(c);
                    }
                }  
            }
            Receive(s);
        }
    }

    public class ConsoleEx
    {
        public static void WriteColor(ConsoleColor c, string str, params object[] args)
        {
            var prec = Console.ForegroundColor;
            Console.ForegroundColor = c;
            Console.Write(str, args);
            Console.ForegroundColor = prec;
        }

        public static void WriteLineColor(ConsoleColor c, string str, params object[] args)
        {
            WriteColor(c, str + "\n", args);
        }
    }
}
