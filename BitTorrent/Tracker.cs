using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace BitTorrent
{
    public class Tracker
    {
        public struct Peer
        {
            public string _peerID;
            public string ip;
            public int port;
        }
        public struct  Response
        {
            public string statusMessage;
            public int interval;
            public int minInterval;
            public string trackerID;
            public int complete;
            public int incomplete;
            public List<Peer> peers;
        };

        private string _trackerURL = String.Empty;
        private string _peerID = String.Empty;
        private MetaInfoFile _torrentFile = null;
        private int _port = 6681;
        private string _ip = "";
        private int _compact = 1;
        private int _noPeerID = 0;
        private int _uploaded = 0;
        private int _downloaded = 0;
        private int _left = 0;
        private string _event = "started";
        private string _key = "";
        private string _trackerID = "";
        private int _numWanted = 1;
        public string PeerID { get => _peerID; set => _peerID = value; }
        public int Port { get => _port; set => _port = value; }

        private Response constructRespone(string connectResult)
        {
            Response response = new Response();


            return (response);

        }

        private byte[] encodeBytesToURL(byte[] toEncode)
        {
            return(WebUtility.UrlEncodeToBytes(toEncode, 0, toEncode.Length));

        }

        private string encodeTrackerURL ()
        {
            string  url = _trackerURL +
            "?info_hash=" + Encoding.ASCII.GetString(encodeBytesToURL(_torrentFile.MetaInfoDict["info hash"])) +
            "&peer_id=" + _peerID +
            "&port=" + _port +
            "&compact=" + _compact +
            "&no_peer_id=" + _noPeerID +
            "&uploaded=" + _uploaded +
            "&downloaded=" + _downloaded +
            "&left=" + _left +
            "&event=" + _event +
            "&ip=" + _ip +
            "&key=" + _key +
            "&trackerid=" + _trackerID +
            "&numwanted=" + _numWanted;

            return (url);

        }

        public Tracker(MetaInfoFile torrentFile, string trackerURL, string peerID)
        {

            _trackerURL = trackerURL;
            _torrentFile = torrentFile;
            _peerID = peerID;

        }


        public Response announce() 
        { 
       
            HttpWebRequest connectRequest = WebRequest.Create(encodeTrackerURL()) as HttpWebRequest;  
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

                return (constructRespone(connectResult));
            }
            else
            {
                Response error = new Response();
                error.statusMessage = connectResponse.StatusDescription;
                return (error);
            }
        }
    }
}
