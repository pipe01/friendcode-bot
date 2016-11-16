using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    class Ftp
    {
        public static void UploadFile(string path)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://pipe01.square7.ch/dbot/" + Path.GetFileName(path));
            request.Method = WebRequestMethods.Ftp.UploadFile;

            request.Credentials = new NetworkCredential("pipe01", Encoding.UTF8.GetString(Convert.FromBase64String("ZmVsaXBpbGxvMTI=")));

            var data = File.ReadAllBytes(path);
            request.ContentLength = data.Length;

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(data, 0, data.Length);
            requestStream.Close();
            requestStream.Dispose();
        }
    }
}
