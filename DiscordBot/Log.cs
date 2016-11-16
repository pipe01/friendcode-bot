using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    class Log
    {
        public static StringBuilder _sb = new StringBuilder();

        public static void Write(string str)
        {
            _sb.AppendLine("[" + DateTime.Now.ToString(@"dd/MM/yyyy HH:mm:ss") + "] " + str);
        }

        public static void Save()
        {
            File.AppendAllText("log.txt", _sb.ToString());
            _sb.Clear();
        }
    }
}
