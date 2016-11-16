using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    public struct Friend
    {
        public FriendCode TDSCode;
        public SteamCode SteamCode;

        public Friend SetSteamCode(SteamCode code)
        {
            return new Friend { SteamCode = code, TDSCode = this.TDSCode };
        }

        public Friend SetTDSCode(FriendCode code)
        {
            return new Friend { SteamCode = this.SteamCode, TDSCode = code };
        }

        public bool Has3DSCode()
        {
            return TDSCode.UserID != 0;
        }

        public bool HasSteamCode()
        {
            return SteamCode.SteamID64 != 0;
        }
    }

    public struct SteamCode
    {
        public long SteamID64;
        public string SteamName;
        public string UserName;

        public override bool Equals(object obj)
        {
            if (!(obj is SteamCode))
                return false;
            var t = (SteamCode)obj;
            return t.SteamID64 == this.SteamID64 && t.SteamName == this.SteamName;
        }

        public override string ToString()
        {
            return this.SteamName;
        }

        public async static Task<SteamCode> GetCode(string steamid)
        {
            var user = await SteamUtils.GetUser(steamid);
            return user.SteamID64 == 0 ? new SteamCode() : new SteamCode { SteamID64 = user.SteamID64, SteamName = user.SteamID, UserName = steamid };
        }
    }

    public struct FriendCode
    {
        public string[] Parts;
        public ulong UserID;

        public FriendCode(string input, ulong userid)
        {
            if (!IsValidFC(input))
                throw new Exception();

            this.UserID = userid;
            this.Parts = new string[3];
            var v = input.Split('-', ' ');
            if (v.Length == 3)
            {
                this.Parts = v;
            }
            else if (input.All(char.IsNumber) && input.Length == 12)
            {
                this.Parts = new[]
                {
                    input.Substring(0, 4),
                    input.Substring(4, 4),
                    input.Substring(8, 4)
                };
            }
        }

        public override string ToString()
        {
            return string.Join("-", this.Parts);
        }

        public static bool IsValidFC(string input)
        {
            return (input.All(o => char.IsNumber(o) || o == ' ' || o == '-')) ||
                   (input.Split('-', ' ').Length == 3) &&
                   (input.Split('-', ' ').All(o => o.Length == 4));
        }
    }
}
