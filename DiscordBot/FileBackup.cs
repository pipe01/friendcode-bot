using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DiscordBot
{
    class FriendsFile : Dictionary<ulong, Friend>
    {
        public new void Add(ulong key, Friend value)
        {
            base.Add(key, value);

        }

        public void SaveToFile()
        {
            var serializer = JsonSerializer.Create();

            string prev = File.ReadAllText("friends.json");
            string n = JsonConvert.SerializeObject(this);

            File.WriteAllText("friends.json", "");
            using (var writer = new StreamWriter("friends.json"))
            {
                serializer.Serialize(writer, this);
            }
        }
    }
}
