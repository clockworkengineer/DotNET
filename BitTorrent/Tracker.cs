using System;
using System.IO;
using System.Net;
using System.Text;

namespace BitTorrent
{
    public class Tracker
    {
        private string _trackerURL = String.Empty;
        private string _peerID = String.Empty;
        private MetaInfoFile _torrentFile = null;
        private int _port = 6681;

        public string PeerID { get => _peerID; set => _peerID = value; }
        public int Port { get => _port; set => _port = value; }

        public Tracker(MetaInfoFile torrentFile, string trackerURL, string peerID)
        {

            _trackerURL = trackerURL;
            _torrentFile = torrentFile;
            _peerID = peerID;

        }

        
        public void printInfoHash(MetaInfoFile metaFile)
        {

            byte[] infoHash = metaFile.MetaInfoDict["info hash"];

            StringBuilder hex = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
                hex.AppendFormat("{0:x2}", b);

            Console.WriteLine(hex);

        }

        public string connect()
        { 
            string infoHash = WebUtility.UrlEncode(Encoding.ASCII.GetString(_torrentFile.MetaInfoDict["info hash"]));
            string connectURL = _trackerURL+"?info_hash=" + infoHash + "&peer_id=" + PeerID + "&port=" + _port.ToString();
            Uri connectWebAddress = new Uri(connectURL);
            HttpWebRequest connectRequest = WebRequest.Create(connectURL) as HttpWebRequest;  
            connectRequest.Method = "GET";
            connectRequest.ContentType = "text/xml";
            string connectResult = string.Empty;
            HttpWebResponse connectResponse;
            using (connectResponse =  connectRequest.GetResponse() as HttpWebResponse)
            {
                StreamReader reader = new StreamReader(connectResponse.GetResponseStream());
                connectResult = reader.ReadToEnd();
            }
            if (connectResponse.StatusCode== HttpStatusCode.OK)
            {
                return (connectResult);
            }
            else
            {
                return (connectResponse.StatusDescription);
            }
        }
    }
}
