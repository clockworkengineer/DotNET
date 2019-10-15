using System;
using System.IO;
using System.Net;

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

        public void printInfoHash(MetaInfoFile metaFile)
        {

            byte[] infoHash = metaFile.MetaInfoDict["info hash"];

            StringBuilder hex = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
                hex.AppendFormat("{0:x2}", b);
            Console.WriteLine(hex);

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
