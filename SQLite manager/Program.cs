using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQLite_manager
{
    class Program
    {
        static SQLiteConnection conn = new SQLiteConnection(@"Data Source=.\database.db;Version=3");

        static void Main()
        {
            if (!File.Exists("database.db"))
            {
                File.Delete("database.db");
            }

            SQLiteConnection.CreateFile("database.db");
            conn.Open();

            ExecuteCmd("CREATE TABLE \"Friends\" ( `UserID` INTEGER NOT NULL UNIQUE, `3DSID` TEXT, `SteamID` TEXT, PRIMARY KEY(`UserID`) )");
            
            var rand = new Random();
            for (int i = 0; i < 30; i++)
            {
                ExecuteCmd($"INSERT INTO Friends ('UserID','3DSID') VALUES ({LongRandom(0, 34965923874, rand)},'{RandAll(rand)}')");
            }

            string sql = "select * from Friends order by UserID desc";
            SQLiteCommand command = new SQLiteCommand(sql, conn);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                Console.WriteLine("UserID: " + reader["UserID"] + "\t3DS Code: " + reader["3DSID"]);

            Console.ReadKey();

            /*Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());*/
        }

        static void ExecuteCmd(string query)
        {
            Console.WriteLine(query);
            var command = new SQLiteCommand(query, conn);
            command.ExecuteNonQuery();
        }

        static string RandPart(Random rand)
        {
            return rand.Next(0, 9) + "" + rand.Next(0, 9) + "" + rand.Next(0, 9) + "" + rand.Next(0, 9);
        }

        static string RandAll(Random rand)
        {
            return RandPart(rand) + "-" + RandPart(rand) + "-" + RandPart(rand);
        }

        static long LongRandom(long min, long max, Random rand)
        {
            long result = rand.Next((Int32)(min >> 32), (Int32)(max >> 32));
            result = (result << 32);
            result = result | (long)rand.Next((Int32)min, (Int32)max);
            return result;
        }
    }
}
