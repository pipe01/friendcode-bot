using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using Ionic.Zip;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BotManager
{
    class Program
    {
        public const string UpdateURL = "http://www.pipe01.square7.ch/dbot/";

        private WebClient _client = new WebClient();
        private Process _p = new Process();
        private static bool _updating = true;
        private static CancellationTokenSource source = new CancellationTokenSource();
        private static CancellationToken token;
        private string _appPath;

        static void Main(string[] args)
        {
            token = source.Token; 
            new Program().MainS();
            token.WaitHandle.WaitOne();
        }

        public void MainS()
        {
            _appPath = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine(_appPath);

            if (!Update())
                Start();
            
            Task.Run(() => {
                while (true)
                {
                    if (!_p.IsRunning())
                    {
                        if (_p.ExitCode == 1)
                        {
                            Start();
                        }
                        else if (!_updating)
                            source.Cancel();
                    }
                    Thread.Sleep(1000);
                }
            }, token);

            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(10000);
                    Update();
                }
            }, token);
        }

        public void Start()
        {
            Console.Clear();

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = _appPath + "bot/DiscordBot.exe",
                UseShellExecute = false,
                RedirectStandardInput = true,
                WorkingDirectory = _appPath + "bot/"
            };
            _p = new Process {StartInfo = psi};
            _p.Start();
            //_p.Exited += (o, e) => source.Cancel();
            
            Task.Run(() => {
                while (true)
                {
                    string inp = Console.ReadLine();
                    _p.StandardInput.WriteLine(inp);
                }
            }, token);
        }

        private bool Update(bool force = false)
        {
            _updating = true;
            bool ret = false;

            if (!_p.IsRunning())
                ConsoleEx.WriteLine("[Manager] Downloading version info");

            try
            {
                _client.DownloadFile(UpdateURL + "dbot.json", "dbot.json");
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("[Manager] An error has occured while downloading version info.\nDetails: " + e.Message);
                return false;
            }

            var latest = new Version(File.ReadAllText("dbot.json"));
            if (!_p.IsRunning())
                ConsoleEx.WriteLine("[Manager] Latest version: {0}", latest);
            File.Delete("dbot.json");

            var curr = new Version(File.ReadAllText("bot/local.json"));
            if (!_p.IsRunning())
                ConsoleEx.WriteLine("[Manager] Current version: {0}", curr);

            if (force || latest > curr)
            {
                if (_p.IsRunning())
                {
                    //_p.CloseMainWindow();
                    _p.StandardInput.WriteLine("exit");
                    _p.WaitForExit(5000);
                    if (_p.IsRunning())
                    {
                        _p.Kill();
                    }
                }
                
                ConsoleEx.WriteLine("[Manager] Update detected\n[Manager] Downloading...");
                _client.DownloadFile(UpdateURL + "dbot-" + latest + ".zip", "update.zip");
                using (var zip = ZipFile.Read("update.zip"))
                {
                    zip.ExtractAll("botu/");
                }
                DirectoryCopy("botu", "bot", true);
                Directory.Delete("botu", true);
                File.Delete("update.zip");
                Start();
                ret = true;
            }

            _updating = false;
            return ret;
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }

    public class ConsoleEx
    {
        public static void WriteLine(string str, params object[] args)
        {
            WriteLineColor(ConsoleColor.Cyan, str, args);
        }

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

    public static class ProcessExtensions
    {
        public static bool IsRunning(this Process process)
        {
            if (process == null)
                throw new ArgumentNullException("process");

            try
            {
                Process.GetProcessById(process.Id);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return false;
            }
            return true;
        }
    }
}
