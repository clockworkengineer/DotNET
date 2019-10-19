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
            public int statusCode;
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
        public string Ip { get => _ip; set => _ip = value; }
        public int Compact { get => _compact; set => _compact = value; }
        public int NoPeerID { get => _noPeerID; set => _noPeerID = value; }
        public int Uploaded { get => _uploaded; set => _uploaded = value; }
        public int Downloaded { get => _downloaded; set => _downloaded = value; }
        public int Left { get => _left; set => _left = value; }
        public string Event { get => _event; set => _event = value; }
        public string Key { get => _key; set => _key = value; }
        public string TrackerID { get => _trackerID; set => _trackerID = value; }
        public int NumWanted { get => _numWanted; set => _numWanted = value; }
        public string TrackerURL { get => _trackerURL; set => _trackerURL = value; }
        public MetaInfoFile TorrentFile { get => _torrentFile; set => _torrentFile = value; }

        private Response constructResponse(byte[] announceResponse)
        {
            Response response = new Response();

            response.statusCode = (int)HttpStatusCode.OK;

            Bencoding.BNodeBase decodedAnnounce= Bencoding.decode(announceResponse);
            Bencoding.BNodeBase field;

            field = Bencoding.getDictionaryEntry(decodedAnnounce, "complete");
            if (field != null)
            {
                response.complete = int.Parse(Encoding.ASCII.GetString(((Bencoding.BNodeNumber)field).number));
            }
            field = Bencoding.getDictionaryEntry(decodedAnnounce, "incomplete");
            if (field != null)
            {

                response.incomplete = int.Parse(Encoding.ASCII.GetString(((Bencoding.BNodeNumber)field).number));
            }
            field = Bencoding.getDictionaryEntry(decodedAnnounce, "peers");
            if (field != null)
            {
                response.peers = new List<Peer>();
                byte[] peers = ((Bencoding.BNodeString)field).str;
                int numberPeers = peers.Length / 6;
                for (var num = 0; num < (peers.Length / 6); num += 6)
                {
                    Peer peer = new Peer();
                    peer._peerID = "";
                    peer.ip = $"{peers[num]}.{peers[num+1]}.{peers[num+2]}.{peers[num+3]}";
                    peer.port = peers[num + 4] * 256 + peers[num + 5];
                    response.peers.Add(peer);
                }

            }
            field = Bencoding.getDictionaryEntry(decodedAnnounce, "interval");
            if (field != null)
            {
                response.interval = int.Parse(Encoding.ASCII.GetString(((Bencoding.BNodeNumber)field).number));

            }
            field = Bencoding.getDictionaryEntry(decodedAnnounce, "min interval");
            if (field != null)
            {
                response.minInterval = int.Parse(Encoding.ASCII.GetString(((Bencoding.BNodeNumber)field).number));
            }
            field = Bencoding.getDictionaryEntry(decodedAnnounce, "tracker id");
            if (field != null)
            {
                response.trackerID = Encoding.ASCII.GetString(((Bencoding.BNodeString)field).str);
            }
 
            field = Bencoding.getDictionaryEntry(decodedAnnounce, "failure reason");
            if (field != null)
            {
                response.statusMessage = Encoding.ASCII.GetString(((Bencoding.BNodeString)field).str);

            }
            field = Bencoding.getDictionaryEntry(decodedAnnounce, "warning message");
            if (field != null)
            {

                response.statusMessage = Encoding.ASCII.GetString(((Bencoding.BNodeString)field).str);
            }

            return (response);

        }

        private byte[] encodeBytesToURL(byte[] toEncode)
        {
            return(WebUtility.UrlEncodeToBytes(toEncode, 0, toEncode.Length));

        }

        private string encodeTrackerURL ()
        {
            string  url = TrackerURL +
            "?info_hash=" + Encoding.ASCII.GetString(encodeBytesToURL(TorrentFile.MetaInfoDict["info hash"])) +
            "&peer_id=" + _peerID +
            "&port=" + Port +
            "&compact=" + Compact +
            "&no_peer_id=" + NoPeerID +
            "&uploaded=" + Uploaded +
            "&downloaded=" + Downloaded +
            "&left=" + Left +
            "&event=" + Event +
            "&ip=" + Ip +
            "&key=" + Key +
            "&trackerid=" + TrackerID +
            "&numwanted=" + NumWanted;

            return (url);

        }

        public Tracker(MetaInfoFile torrentFile, string trackerURL, string peerID)
        {

            TrackerURL = trackerURL;
            TorrentFile = torrentFile;
            PeerID = peerID;

        }


        public Response announce() 
        { 
       
            HttpWebRequest httpGetRequest = WebRequest.Create(encodeTrackerURL()) as HttpWebRequest;
            HttpWebResponse httpGetResponse;
            byte[] announceResponse;

            httpGetRequest.Method = "GET";
            httpGetRequest.ContentType = "text/xml";

            using (httpGetResponse =  httpGetRequest.GetResponse() as HttpWebResponse)
            {
                StreamReader reader = new StreamReader(httpGetResponse.GetResponseStream());
                using (var memstream = new MemoryStream())
                {
                    reader.BaseStream.CopyTo(memstream);
                    announceResponse = memstream.ToArray();
                }
            }
            if (httpGetResponse.StatusCode== HttpStatusCode.OK)
            {

                return (constructResponse(announceResponse));
            }
            else
            {
                Response error = new Response();
                error.statusCode = (int)httpGetResponse.StatusCode;
                error.statusMessage = httpGetResponse.StatusDescription;
                return (error);
            }
        }
    }
}
