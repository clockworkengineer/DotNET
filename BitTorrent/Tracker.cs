using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Timers;

namespace BitTorrent
{
    public class Tracker
    {
        private static System.Timers.Timer _announceTimer;

        public enum TrackerEvent
        {
            started = 0,
            stopped,
            completed
        };

        public struct Peer
        {
            public string _peerID; 
            public string ip;
            public int port;
        }

        public struct  Response
        {
            public int announceCount;
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
        private Response _currentTrackerResponse;
        private int _port = 6681;
        private string _ip = String.Empty;
        private int _compact = 1;
        private int _noPeerID = 0;
        private int _uploaded = 0;
        private int _downloaded = 0;
        private int _left = 0;
        private TrackerEvent _event = TrackerEvent.started;
        private string _key = String.Empty;
        private string _trackerID = String.Empty;
        private int _numWanted = 1;
        private int _interval = 0;

        public string PeerID { get => _peerID; set => _peerID = value; }
        public int Port { get => _port; set => _port = value; }
        public string Ip { get => _ip; set => _ip = value; }
        public int Compact { get => _compact; set => _compact = value; }
        public int NoPeerID { get => _noPeerID; set => _noPeerID = value; }
        public int Uploaded { get => _uploaded; set => _uploaded = value; }
        public int Downloaded { get => _downloaded; set => _downloaded = value; }
        public int Left { get => _left; set => _left = value; }
        public TrackerEvent Event { get => _event; set => _event = value; }
        public string Key { get => _key; set => _key = value; }
        public string TrackerID { get => _trackerID; set => _trackerID = value; }
        public int NumWanted { get => _numWanted; set => _numWanted = value; }
        public string TrackerURL { get => _trackerURL; set => _trackerURL = value; }
        public MetaInfoFile TorrentFile { get => _torrentFile; set => _torrentFile = value; }
        public static Timer AnnounceTimer { get => _announceTimer; set => _announceTimer = value; }
        public int Interval { get => _interval; set => _interval = value; }
        public Response CurrentTrackerResponse { get => _currentTrackerResponse; set => _currentTrackerResponse = value; }

        private Response constructResponse(byte[] announceResponse)
        {
            Response response = new Response();

            response.statusCode = (int)HttpStatusCode.OK;

            BNodeBase decodedAnnounce= Bencoding.decode(announceResponse);

            int.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "complete"), out response.complete);
            int.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "incomplete"), out response.incomplete);

            BNodeBase field = Bencoding.getDictionaryEntry(decodedAnnounce, "peers");
            if (field != null)
            {
                response.peers = new List<Peer>();
                if (field is BNodeString)
                {
                    byte[] peers = ((BNodeString)field).str;
                    int numberPeers = peers.Length / 6;
                    for (var num = 0; num < (peers.Length / 6); num += 6)
                    {
                        Peer peer = new Peer();
                        peer._peerID = String.Empty;
                        peer.ip = $"{peers[num]}.{peers[num + 1]}.{peers[num + 2]}.{peers[num + 3]}";
                        peer.port = peers[num + 4] * 256 + peers[num + 5];
                        response.peers.Add(peer);
                    }
                }
                else if (field is BNodeList)
                {
                    foreach (var listItem in ((BNodeList)(field)).list)
                    {
                        if (listItem is BNodeDictionary)
                        {
                            Peer peer = new Peer();
                            BNodeBase peerDictionaryItem = ((BitTorrent.BNodeDictionary)listItem);
                            BNodeBase peerField = Bencoding.getDictionaryEntry(peerDictionaryItem, "ip");
                            if (peerField != null)
                            {
                                string path = string.Empty;
                                peer.ip = Encoding.ASCII.GetString(((BitTorrent.BNodeString)peerField).str);
                            }
                            peerField = Bencoding.getDictionaryEntry(peerDictionaryItem, "port");
                            if (peerField != null)
                            {
                                string path = string.Empty;
                                peer.port = int.Parse(Encoding.ASCII.GetString(((BitTorrent.BNodeNumber)peerField).number));
                            }
                            response.peers.Add(peer);
                        }
                    }
                }

            }

            int.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "interval"), out response.interval);
            int.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "min interval"), out response.minInterval);
            response.trackerID = Bencoding.getDictionaryEntryString(decodedAnnounce, "tracker id");
            response.statusMessage = Bencoding.getDictionaryEntryString(decodedAnnounce, "failure reason");
            response.statusMessage = Bencoding.getDictionaryEntryString(decodedAnnounce, "warning message");

            response.announceCount++;

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

        private static void OnAnnounceEvent(Object source, ElapsedEventArgs e, Tracker tracker)
        {
            tracker.CurrentTrackerResponse = tracker.announce();
            MainClass.annouceResponse(tracker._currentTrackerResponse);

        }

        public Tracker(MetaInfoFile torrentFile, string peerID)
        {

            _trackerURL = torrentFile.getTrackerURL();
            _torrentFile = torrentFile;
            _peerID = peerID;

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

        public void startAnnouncing()
        {
            _announceTimer = new System.Timers.Timer(_interval);
            _announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(sender, e, this);
            _announceTimer.AutoReset = true;
            _announceTimer.Enabled = true;
        }

        public void stopAnnonncing()
        {
            _announceTimer.Stop();
            _announceTimer.Dispose();
        }

        public void update(Response response)
        {
            int oldInterval = _interval;
            if (response.minInterval != 0)
            {
                _interval = response.minInterval;
            }
            else
            {
                _interval = response.interval;
            }
            _trackerID = response.trackerID;
            if (oldInterval!=_interval)
            {
                stopAnnonncing();
                startAnnouncing();
            }
        }
    }
}
