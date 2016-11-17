using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;

namespace DiscordBot
{
    class Program
    {
        static int Main(string[] args)
        {
            int ret = 0;
            try
            {
                ret = Instance.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
                ret = 1;
                Thread.Sleep(2000);
            }
            return ret;
        }

        public static RemoteControl Remote = new RemoteControl();
        public static Program Instance = new Program();

        private static DiscordClient _client;
        //private Dictionary<ulong, FriendCode> _codes = new Dictionary<ulong, FriendCode>();
        private Dictionary<ulong, Friend> _friends = new Dictionary<ulong, Friend>();
        private ConfigFile _config;
        private bool _botReady = false, debug = false;
        private IExitSignal ExitSignal;
        public int ExitCode;

        public int Start()
        {
            Remote.Start();

            Log._sb.AppendLine("\nSTART\n");

            try
            {
                if (IsRunningOnMono())
                {
                    ConsoleEx.WriteLineColor(ConsoleColor.Yellow, "Mono detected");
                    ExitSignal = new UnixExitSignal();
                }
                else
                {
                    ExitSignal = new WinExitSignal();
                }
            }
            catch (TypeInitializationException)
            {
                ExitSignal = new WinExitSignal();
            }
            ExitSignal.Exit += async (o, n) => await Exit();

            var version = File.ReadAllText("local.json");
            ConsoleEx.WriteLineColor(ConsoleColor.Yellow, "Running pipe0481's friend bot v{0}", version);

            LoadConfig();

            _client = new DiscordClient(x => { x.LogLevel = LogSeverity.Info; });
            _client.Log.Message += (s, e) => ConsoleEx.WriteLine($"[{e.Severity}] {e.Source}: {e.Message}");
            
            _client.MessageReceived += async (s, e) => {
                try
                {
                    if (e.Server != null && _config.Channels != null && !_config.Channels.ContainsKey(e.Server.Id))
                    {
                        _config.Channels.Add(e.Server.Id, e.Server.DefaultChannel.Id);
                    }

                    if (e.Server == null || e.Channel.Id != _config.Channels[e.Server.Id] || e.User.Id == _client.CurrentUser.Id)
                        return;

                    try
                    {
                        if (await RegisterSteamCode(e) || await Register3DSCode(e))
                        {
                            Console.WriteLine(e.User.Name + ": " + e.Message.Text);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }

                    if (!e.Message.Text.StartsWith("!fc"))
                        return;

                    string cmd = e.Message.Text.Substring(1, (e.Message.Text.Contains(' ') ? e.Message.Text.IndexOf(' ') : e.Message.Text.Length) - 1);
                    string rest = e.Message.Text.Contains(' ') ? e.Message.Text.Substring(e.Message.Text.IndexOf(' ') + 1) : "";

                    bool ret = await ExecuteCmd(cmd, rest, e);
                    if (!ret) //If it isn't succesful
                    {
                        //Warn the user
                        await e.Channel.SendMessage(e.User.Mention + ", I don't understand that");
                    }

                    if (this.debug)
                        ConsoleEx.WriteLineColor(ret ? ConsoleColor.Green : ConsoleColor.Red, "User @{0} issued command !{1} {2}", e.User.Name, cmd, rest);
                }
                catch (Exception ex)
                {
                    ConsoleEx.WriteLineColor(ConsoleColor.Red, ex.Message);
                    await e.Channel.SendMessage($"`{ex.Message}`\nRestarting bot...");
                    ExitCode = 1;
                    await Exit();
                }
            };
            
            Task.Run(async () => {
                while (_client.State != ConnectionState.Connected)
                {
                    ConsoleEx.WriteLine("Connecting...");
                    Thread.Sleep(1000);
                }
                Thread.Sleep(1000);
                _botReady = true;
                _client.SetGame("Version " + version);
                ConsoleEx.WriteLineColor(ConsoleColor.Green, "Connected");
                int i = 0;
                while (_client.State == ConnectionState.Connected)
                {
                    Thread.Sleep(1000);
                    i++;
                    if (i == 30)
                        SaveConfig();
                }
                await Exit();
            });
            
            Task.Run(async () => await CommandTask());

            _client.ExecuteAndWait(async () => {
                await _client.Connect("MjQ1MzAzMDgwOTMxODE5NTIx.Cwpb_A.HHrQuuMuHWu0tFwzkqhZ3H-Nj14", TokenType.Bot);
            });
            
            return ExitCode;
        }

        private async Task<bool> RegisterSteamCode(MessageEventArgs e)
        {
            string msg = e.Message.Text;
            var user = e.User;

            var scode = Regex.Match(msg, "(?<=\\b(s|S)team:\\s?)(\\w+)");


            if (!scode.Success) return false;

            if (!_friends.ContainsKey(user.Id))
                _friends.Add(user.Id, new Friend());

            if (!_friends[user.Id].SteamCode.Equals(new SteamCode()))
            {
                await e.Channel.SendMessage(user.Mention + ", you already registered your steam ID! Type `!fcremove steam` to change it.");
                return false;
            }

            var code = await SteamCode.GetCode(scode.Value);
            if (code.SteamID64 == 0)
            {
                await e.Channel.SendMessage(user.Mention + ", that steam ID doesn't exist! D:");
                return false;
            }
            _friends.Set(user.Id, _friends[user.Id].SetSteamCode(code));
            await e.Channel.SendMessage(user.Mention + $", your steamID64 is {_friends[user.Id].SteamCode.SteamID64}, and your profile name is {_friends[user.Id].SteamCode.SteamName}. If this isn't right, type !fcremove steam");

            return true;
        }

        private async Task<bool> Register3DSCode(MessageEventArgs e)
        {
            var tds = Regex.Match(e.Message.Text, "([0-9]{4}-){2}[0-9]{4}");

            if (tds.Success && FriendCode.IsValidFC(tds.Value)) //If match
            {
                if (_friends.GetOrAdd(e.User.Id).Has3DSCode())
                {
                    await e.Channel.SendMessage(e.User.Mention + ", you have already registered your 3DS friend code! Type `!fcremove 3ds` to change it.");
                    return false;
                }
                this._friends.Set(e.User.Id, this._friends[e.User.Id].SetTDSCode(new FriendCode(tds.Value, e.User.Id)));
                await e.Channel.SendMessage(e.User.Mention + $", your 3DS friend code is now *{tds.Value}*");
                return true;
            }

            return false;
        }

        private async Task CommandTask()
        {
            while (!_botReady)
                Thread.Sleep(1000);

            while (true)
            {
                string input = Console.ReadLine();
                await ConsoleCmd(input);
            }
        }

        public async Task ConsoleCmd(string input)
        {
            switch (input)
            {
                case "help":
                    var cmds = new[]
                    {
                        "help", "exit", "listchannels", "debug on/off", "printlog"
                    };
                    ConsoleEx.WriteLineColor(ConsoleColor.Cyan, "Available commands: " + string.Join(", ", cmds));
                    break;
                case "exit":
                    await Exit();
                    break;
                case "listchannels":
                    foreach (var item in _client.Servers)
                    {
                        ConsoleEx.WriteLineColor(ConsoleColor.Yellow, "{0}:", item.Name);
                        foreach (var chan in item.AllChannels)
                        {
                            ConsoleEx.WriteLineColor(ConsoleColor.DarkYellow, "  {0}: {1}", chan.Name, chan.Id);
                        }
                    }
                    break;
                case "debug off":
                    debug = false;
                    ConsoleEx.WriteLineColor(ConsoleColor.Green, "Debugging off");
                    break;
                case "debug on":
                    debug = true;
                    ConsoleEx.WriteLineColor(ConsoleColor.Green, "Debugging on");
                    break;
                case "printlog":
                    ConsoleEx.WriteLineColor(ConsoleColor.Yellow, "---Log---");
                    Console.Write(File.ReadAllText("log.txt"));
                    ConsoleEx.WriteLineColor(ConsoleColor.Yellow, "---------");
                    break;
            }
        }

        public async Task<bool> ExecuteCmd(string cmd, string rest, MessageEventArgs e)
        {
            switch (cmd)
            {
                case "fcerr":
                    if (!CanManage(e.User))
                    {
                        await e.Channel.SendMessage(e.User.Mention + ", you are not qualified for this!");
                        break;
                    }
                    throw new Exception();
                case "fclist":
                    if (_friends.Count == 0)
                    {
                        await e.Channel.SendMessage("The friend codes list is empty! You can help by adding yours");
                        break;
                    }

                    await e.Channel.SendMessage(e.User.Mention + ", check your direct messages!");

                    await e.User.SendMessage("Hey, did you ask me for all the friend codes? Well, here you have 'em!\nMake sure to add everyone!\n---------------");
                    var sb = new StringBuilder();
                    //var servers = _config.GetServers(_client);
                    foreach (var item in _friends)
                    {
                        var us = e.Server?.GetUser(item.Key);
                        if (us == null)
                            continue;
                        sb.AppendLine($"**@{us.Name}**'s friend codes:");
                        if (item.Value.Has3DSCode())
                        {
                            sb.AppendLine($"  Their 3DS friend code is **{item.Value.TDSCode}**");
                        }
                        if (item.Value.HasSteamCode())
                        {
                            sb.AppendLine($"  Their Steam user name is **{item.Value.SteamCode}**");
                        }
                    }
                    await e.User.SendMessage(sb.ToString());
                    break;
                case "fcsetchannel":
                    if (rest == "")
                        break;
                    // ReSharper disable once SimplifyLinqExpression
                    if (!CanManage(e.User))
                    {
                        await e.Channel.SendMessage(e.User.Mention + ", you are not qualified for this!");
                        break;
                    }
                    if (e.Server != null && !e.Message.MentionedChannels.Any())
                    {
                        await e.Channel.SendMessage(e.User.Mention + ", the specified channel doesn't exist!");
                        break;
                    }

                    _config.Channels[e.Server.Id] = e.Message.MentionedChannels.First().Id;

                    await e.Channel.SendMessage("Channel set to " + rest);
                    break;
                case "fcshutdown":
                    if (!CanManage(e.User))
                    {
                        await e.Channel.SendMessage(e.User.Mention + ", you are not qualified for this!");
                        break;
                    }
                    await e.Channel.SendMessage("Shutting down... y u do dis to me");
                    await Exit();
                    break;
                case "fcrestart":
                    if (!CanManage(e.User))
                    {
                        await e.Channel.SendMessage(e.User.Mention + ", you are not qualified for this!");
                        break;
                    }
                    await e.Channel.SendMessage("Restarting bot...");
                    ExitCode = 1;
                    await Exit();
                    break;
                case "fcget":
                    Friend friend;
                    User user = e.Message.MentionedUsers.Any() ? e.Message.MentionedUsers.First() : e.User;
                    if (!_friends.TryGetValue(user.Id, out friend))
                    {
                        await e.Channel.SendMessage(e.User.Mention + ", that user hasn't registered anything yet!");
                        return true;
                    }
                    switch (rest.ToLower())
                    {
                        case "3ds":
                            if (friend.Has3DSCode())
                            {
                                await e.Channel.SendMessage($"{e.User.Mention}, @{user.Name}'s 3DS friend code is **{friend.TDSCode}**");
                            }
                            break;
                        case "steam":
                            if (friend.HasSteamCode())
                            {
                                await e.Channel.SendMessage($"{e.User.Mention}, @{user.Name}'s Steam friend code is **{friend.SteamCode}**");
                            }
                            break;
                        default:
                            if (!(friend.Has3DSCode() || friend.HasSteamCode()))
                                break;
                            var strb = new StringBuilder();
                            strb.AppendLine($"{e.User.Mention}: @{user.Name}'s information:");
                            if (friend.Has3DSCode())
                            {
                                strb.AppendLine($"Their 3DS friend code is *{friend.TDSCode}*");
                            }
                            if (friend.HasSteamCode())
                            {
                                strb.AppendLine($"Their Steam user name is *{friend.SteamCode}*");
                            }
                            await e.Channel.SendMessage(strb.ToString());
                            break;
                    }
                    break;
                case "fcremove":
                    if (rest == "")
                    {
                        await e.Channel.SendMessage(e.User.Mention + ", which friend code do you want to remove? (steam, 3ds)");
                    }
                    else if (rest == "3ds")
                    {
                        if (!_friends.ContainsKey(e.User.Id) || _friends[e.User.Id].TDSCode.UserID == 0)
                        {
                            await e.Channel.SendMessage(e.User.Mention + ", you haven't registered your 3DS friend code yet!");
                            break;
                        }
                        _friends[e.User.Id] = _friends[e.User.Id].SetTDSCode(new FriendCode());
                        await e.Channel.SendMessage("Removed your 3DS friend code from the database!");
                    }
                    else if (rest == "steam")
                    {
                        if (_friends[e.User.Id].SteamCode.SteamID64 == 0)
                        {
                            await e.Channel.SendMessage(e.User.Mention + ", you haven't registered your Steam friend code yet!");
                            break;
                        }
                        _friends[e.User.Id] = _friends[e.User.Id].SetSteamCode(new SteamCode());
                        await e.Channel.SendMessage("Removed your Steam friend code from the database!");
                    }
                    break;
                case "fchelp":
                case "fcinfo":
                    await e.Channel.SendMessage(e.User.Mention + ", this is pipe0481's bot designed to register 3DS and Steam friend codes, so that everyone can interact with eachother\n" +
                           "To register your 3DS friend code, you can type the following:\n" +
                           "`FC: XXXX-YYYY-ZZZZ`\n" +
                           "Or just simply:\n" +
                           "`XXXX-YYYY-ZZZZ`\n" +
                           "To register your Steam account, type:\n" +
                           "Steam: MySteamName\n" +
                           "To get someone's friend codes, use `!fcget ` and mention someone\n" +
                           "To change your friend codes, type `!fcremove 3ds/steam` and enter your friend code again\n" +
                           "If you want to get all the registered friend codes, use `!fclist`");

                    break;
                default:
                    return false;
            }
            return true;
        }

        private async Task Exit()
        {
            SaveConfig();
            ConsoleEx.WriteLineColor(ConsoleColor.DarkRed, "Disconnecting...");
            await _client.Disconnect();
        }

        private bool CanManage(User user)
        {
            return user.Roles.Any(o => o.Permissions.ManageRoles) || user.Name == "pipe0481";
        }

        private async void LoadConfig()
        {
            if (!File.Exists("config.json") || File.ReadAllText("config.json") == "")
            {
                _config = new ConfigFile();
                File.WriteAllText("config.json", JsonConvert.SerializeObject(_config));
            }

            new WebClient().DownloadFile("http://pipe01.square7.ch/dbot/friends.json", "friends.json");
            if (!File.Exists("friends.json"))
            {
                File.WriteAllText("friends.json", JsonConvert.SerializeObject(new Dictionary<ulong, Friend>()));
            }

            
            string configf = File.ReadAllText("config.json");
            _config = JsonConvert.DeserializeObject<ConfigFile>(configf);

            string codesf = File.ReadAllText("friends.json");
            _friends = JsonConvert.DeserializeObject<Dictionary<ulong, Friend>>(codesf);

            ConsoleEx.WriteLineColor(ConsoleColor.Cyan, "Loading steam users...");
            int count = _friends.Count(o => o.Value.HasSteamCode());
            for (int i = 0; i < _friends.Count; i++)
            {
                if (_friends.Values.ElementAt(i).HasSteamCode())
                {
                    ConsoleEx.WriteLineColor(ConsoleColor.DarkCyan, $"{i + 1}/{count}");
                    _friends.Set(_friends.Keys.ElementAt(i),
                        _friends[_friends.Keys.ElementAt(i)].SetSteamCode(await SteamCode.GetCode(_friends.Values.ElementAt(i).SteamCode.SteamName)));
                }
            }
        }
        private void SaveConfig()
        {
            var serializer = JsonSerializer.Create();

            File.WriteAllText("config.json", "");
            using (var writer = new StreamWriter("config.json"))
            {
                serializer.Serialize(writer, _config);
            }

            File.WriteAllText("friends.json", "");
            using (var writer = new StreamWriter("friends.json"))
            {
                serializer.Serialize(writer, _friends);
            }

            Ftp.UploadFile("friends.json");

            Log.Save();
        }

        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        public static async Task RunActionCatch(Func<Task> action, Action<Exception> cat)
        {
            await Task.Run(async () => {
                try
                {
                    await action.Invoke();
                }
                catch (Exception e)
                {
                    cat.Invoke(e);
                }
            });
        }
    }

    public class ConfigFile
    {
        public Dictionary<ulong, ulong> Channels = new Dictionary<ulong, ulong>();

        public IEnumerable<Server> GetServers(DiscordClient client)
        {
            return this.Channels.Select(item => client.GetServer(item.Key));
        }
    }

    public class ConsoleEx
    {
        public static void WriteColor(ConsoleColor c, string str, params object[] args)
        {
            WriteColor(c, str, true, args);
        }
        public static void WriteColor(ConsoleColor c, string str, bool send, params object[] args)
        {
            var prec = Console.ForegroundColor;
            var fstr = string.Format(str, args);
            Console.ForegroundColor = c;
            Console.Write(fstr);
            Console.ForegroundColor = prec;
            if (send)
            {
                Program.Remote.Send("@" + c.ToString("X").Last());
                Program.Remote.Send(fstr.Replace("\n","<nl>"));
            }
            Log.Write(fstr);
        }

        public static void WriteLineColor(ConsoleColor c, string str, params object[] args)
        {
            var fstr = string.Format(str, args);
            WriteColor(c, fstr + "\n", false);
            Program.Remote.Send("@" + c.ToString("X").Last());
            Program.Remote.Send(fstr + "\n");
        }

        public static void WriteLine(string str, params object[] args)
        {
            var fstr = string.Format(str, args);
            Console.WriteLine(fstr);
            Program.Remote.Send(fstr + "\n");
            Log.Write(fstr);
        }
    }

    public static class Extensions
    {
        public static void Set<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue val, bool add = true)
        {
            if (!dic.ContainsKey(key))
            {
                if (add)
                    dic.Add(key, val);
            }
            else
                dic[key] = val;
        }

        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key)
        {
            if (!dic.ContainsKey(key))
                dic.Add(key, default(TValue));
            return dic[key];
        }
    }
}
