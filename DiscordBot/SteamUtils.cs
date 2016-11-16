using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot
{
    class SteamUtils
    {
        public struct SteamUser
        {
            public long SteamID64;
            public string SteamID;
        }

        public async static Task<SteamUser> GetUser(string username)
        {
            var ret = new SteamUser();
            var wc = new WebClient();
            string resp = await wc.DownloadStringTaskAsync($"http://steamcommunity.com/id/{username}/?xml=1");

            if (resp.Contains("<error>")) return ret;

            var s64 = new Regex("\\d+(?=<\\/steamID64>)").Match(resp);
            if (s64.Success)
            {
                ret.SteamID64 = long.Parse(s64.Value);
            }

            var sid = new Regex("(?<=<!\\[CDATA\\[\\s?)(.*)(?=\\s?]]>)").Match(resp);
            if (sid.Success)
            {
                ret.SteamID = sid.Value;
            }

            return ret;
        }
    }
}
