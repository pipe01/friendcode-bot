using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Uploader
{
    class Program
    {
        public const string RootFolder = @".\Release";

        static void Main(string[] args)
        {
            if (!Directory.Exists(RootFolder))
                return;

            string curr = File.Exists(RootFolder + "/local.json") ? File.ReadAllText(RootFolder + "/local.json") : "0.0.0.0";
            ConsoleEx.WriteLineColor(ConsoleColor.DarkYellow, "\n\nLocal version: {0}", curr);
            string version;
            do
            {
                ConsoleEx.WriteColor(ConsoleColor.Yellow, "\nWhat version is this?: ");
                version = Console.ReadLine();
            } while (!Regex.IsMatch(version, @"(\d+.){3}\d+"));

            File.WriteAllText(RootFolder + "/local.json", version);

            ConsoleEx.WriteLineColor(ConsoleColor.DarkYellow, "\n--- Enumerate files");
            var efiles = Directory.EnumerateFiles(RootFolder, "*.*", SearchOption.AllDirectories);
            List<string> files = new List<string>();

            Console.Write("Include libraries? (y/N): ");
            var key = Console.ReadKey();
            Console.WriteLine();
            if (key.Key == ConsoleKey.Y)
                foreach (var item in efiles)
                {
                    Console.Write(item);
                    if (!(item.EndsWith(".xml") ||
                          item.EndsWith(".pdb") ||
                          item.EndsWith(".manifest") ||
                          (item.EndsWith(".json") && !item.Contains("local.json")) ||
                          item.Contains("vshost")))
                    {
                        files.Add(item);
                        Console.WriteLine(": OK");
                    }
                    else
                    {
                        Console.WriteLine(": NO");
                    }
                }
            else
                files.AddRange(new[]{ RootFolder + "\\DiscordBot.exe", RootFolder + "\\local.json" });

            ConsoleEx.WriteLineColor(ConsoleColor.DarkYellow, "\n--- Copy files");
            Directory.CreateDirectory(".\\ziptmp");
            foreach (var item in files)
            {
                Console.Write(item + " ");
                try
                {
                    File.Copy(item, @".\ziptmp\" + item.Replace(RootFolder, ""));
                    ConsoleEx.WriteLineColor(ConsoleColor.Green, "OK");
                }
                catch (Exception e)
                {
                    ConsoleEx.WriteLineColor(ConsoleColor.Red, "Failed: " + e.Message);
                }
            }

            ConsoleEx.WriteLineColor(ConsoleColor.DarkYellow, "\n--- Zip files");
            try
            {
                ZipFile.CreateFromDirectory("ziptmp", "update.zip");
                ConsoleEx.WriteLineColor(ConsoleColor.Green, "OK");
            }
            catch (IOException e)
            {
                ConsoleEx.WriteLineColor(ConsoleColor.Red, "Failed: " + e.Message);
                Thread.Sleep(2000);
                Clean();
                return;
            }
            
            ConsoleEx.WriteLineColor(ConsoleColor.DarkYellow, "\n--- Upload zip");
            #region Upload
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://pipe01.square7.ch/dbot/dbot-" + version + ".zip");
            request.Method = WebRequestMethods.Ftp.UploadFile;

            request.Credentials = new NetworkCredential("pipe01", Encoding.UTF8.GetString(Convert.FromBase64String("ZmVsaXBpbGxvMTI=")));

            FileStream file = File.OpenRead("update.zip");
            request.ContentLength = file.Length;

            int splength = (int)Math.Ceiling(((float)file.Length) / 100);

            Stream requestStream = request.GetRequestStream();

            int count = 0;
            byte[] buffer = new byte[8192];
            long length = 0;

            string max = SizeSuffix(file.Length);
            ConsoleEx.WriteColor(ConsoleColor.Magenta, "Progress: ");
            int cx = Console.CursorLeft, cy = Console.CursorTop;

            while ((count = file.Read(buffer, 0, 8192)) != 0)
            {
                length += count;
                requestStream.Write(buffer, 0, count);
                Console.SetCursorPosition(cx, cy);
                Console.Write("{0}/{1}      ", SizeSuffix(length), max);
            }

            requestStream.Close();
            file.Close();

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            response.Close();

            file.Dispose();

            ConsoleEx.WriteLineColor(ConsoleColor.Green, "\n\nUpload succesfully completed");
            #endregion

            ConsoleEx.WriteLineColor(ConsoleColor.DarkYellow, "\n--- Upload info");
            request = (FtpWebRequest)WebRequest.Create("ftp://pipe01.square7.ch/dbot/dbot.json");
            request.Method = WebRequestMethods.Ftp.UploadFile;

            request.Credentials = new NetworkCredential("pipe01", Encoding.UTF8.GetString(Convert.FromBase64String("ZmVsaXBpbGxvMTI=")));

            byte[] data = Encoding.UTF8.GetBytes(version);
            request.ContentLength = data.Length;

            requestStream = request.GetRequestStream();
            requestStream.Write(data, 0, data.Length);
            requestStream.Close();
            requestStream.Dispose();



            ConsoleEx.WriteLineColor(ConsoleColor.Green, "\n\nAll done!");
            Thread.Sleep(2000);

            Clean();
        }

        static void Clean()
        {
            Directory.Delete("ziptmp", true);
            File.Delete("update.zip");
        }

        static readonly string[] SizeSuffixes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(long value)
        {
            if (value < 0)
            { return "-" + SizeSuffix(-value); }
            if (value == 0)
            { return "0.0 bytes"; }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return $"{adjustedSize:n1} {SizeSuffixes[mag]}";
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
