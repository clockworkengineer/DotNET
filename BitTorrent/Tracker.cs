using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
namespace BitTorrent
{
    public class Tracker
    {
        private string _trackerURL = String.Empty;

        public Tracker(string trackerURL)
        {

            _trackerURL = trackerURL;

        }

        public void createInfoHash(MetaInfoFile metaFile)
        {

            //string buffer = string.Empty;
            //buffer = Encoding.ASCII.GetString(metaFile.MetaInfoDict["name"]);
            //buffer += metaFile.MetaInfoDict["length"];
            //buffer += metaFile.MetaInfoDict["piece length"];
            //buffer += metaFile.MetaInfoDict["pieces"];

            //string tmp = metaFile.MetaInfoDict["pieces"];

            //byte[] data = new byte[buffer.Length];

            //for (var i=0;  i <buffer.Length; i++){
            //    data[i] = (byte) buffer[i];
            //}

            //byte[] result;

            //SHA1 sha = new SHA1CryptoServiceProvider();

            //result = sha.ComputeHash(data);

            //StringBuilder hex = new StringBuilder(result.Length);
            //foreach (byte b in result)
            //    hex.AppendFormat("{0:x2}", b);
            //Console.WriteLine(hex);

        }

        public void connect()
        {
            string html = string.Empty;
          
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_trackerURL);
            request.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }

            Console.WriteLine(html);

        }
    }
}
