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

        public enum TrackerEvent
        {
            started = 0,
            stopped,
            completed
        };

        private System.Timers.Timer _announceTimer;
        private string _trackerURL = String.Empty;
        private string _peerID = String.Empty;
        private MetaInfoFile _torrentFile = null;
        private AnnounceResponse _currentTrackerResponse;
        private UInt32 _port = 6681;
        private string _ip = String.Empty;
        private UInt32 _compact = 1;
        private UInt32 _noPeerID = 0;
        private UInt64 _uploaded = 0;
        private UInt64 _downloaded = 0;
        private UInt64 _left = 0;
        private TrackerEvent _event;
        private string _key = String.Empty;
        private string _trackerID = String.Empty;
        private UInt32 _numWanted = 1;
        private UInt32 _interval = 2000;

        public UInt64 Uploaded { get => _uploaded; set => _uploaded = value; }
        public UInt64 Downloaded { get => _downloaded; set => _downloaded = value; }
        public UInt64 Left { get => _left; set => _left = value; }
        public TrackerEvent Event { get => _event; set => _event = value; }

        private AnnounceResponse constructResponse(byte[] announceResponse, ref AnnounceResponse response)
        {

            response.statusCode = (int)HttpStatusCode.OK;

            BNodeBase decodedAnnounce= Bencoding.decode(announceResponse);

            UInt32.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "complete"), out response.complete);
            UInt32.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "incomplete"), out response.incomplete);

            BNodeBase field = Bencoding.getDictionaryEntry(decodedAnnounce, "peers");
            if (field != null)
            {
                response.peers = new List<PeerDetails>();
                if (field is BNodeString)
                {
                    byte[] peers = ((BNodeString)field).str;
                    UInt32 numberPeers = (UInt32) peers.Length / 6;
                    for (var num = 0; num < (peers.Length / 6); num += 6)
                    {
                        PeerDetails peer = new PeerDetails();
                        peer._peerID = String.Empty;
                        peer.ip = $"{peers[num]}.{peers[num + 1]}.{peers[num + 2]}.{peers[num + 3]}";
                        peer.port = (UInt32) peers[num + 4] * 256 + peers[num + 5];
                        response.peers.Add(peer);
                    }
                }
                else if (field is BNodeList)
                {
                    foreach (var listItem in ((BNodeList)(field)).list)
                    {
                        if (listItem is BNodeDictionary)
                        {
                            PeerDetails peer = new PeerDetails();
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
                                peer.port = UInt32.Parse(Encoding.ASCII.GetString(((BitTorrent.BNodeNumber)peerField).number));
                            }
                            response.peers.Add(peer);
                        }
                    }
                }

            }

            UInt32.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "interval"), out response.interval);
            UInt32.TryParse(Bencoding.getDictionaryEntryString(decodedAnnounce, "min interval"), out response.minInterval);
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
            "&uploaded=" + Uploaded +
            "&downloaded=" + Downloaded +
            "&left=" + Left +
            "&event=" + Event +
            "&ip=" + _ip +
            "&key=" + _key +
            "&trackerid=" + _trackerID +
            "&numwanted=" + _numWanted;

            return (url);

        }

        private static void OnAnnounceEvent(Object source, ElapsedEventArgs e, Tracker tracker)
        {
            tracker._currentTrackerResponse = tracker.announce();
            Program.annouceResponse(tracker._currentTrackerResponse);

        }

        public Tracker(MetaInfoFile torrentFile, string peerID)
        {

            _trackerURL = torrentFile.getTrackerURL();
            _torrentFile = torrentFile;
            _peerID = peerID;

        }

        public AnnounceResponse announce() 
        {

            Program.Logger.Info($"Announce: info_hash={Encoding.ASCII.GetString(encodeBytesToURL(_torrentFile.MetaInfoDict["info hash"]))} " +
                  $"peer_id={_peerID} port={_port} compact={_compact} no_peer_id={_noPeerID} uploaded={Uploaded}" +
                  $"downloaded={Downloaded} left={Left} event={Event} ip={_ip} key={_key} trackerid={_trackerID} numwanted={_numWanted}");

            AnnounceResponse response = new AnnounceResponse();

            try
            {
                HttpWebRequest httpGetRequest = WebRequest.Create(encodeTrackerURL()) as HttpWebRequest;
                HttpWebResponse httpGetResponse;
                byte[] announceResponse;

                httpGetRequest.Method = "GET";
                httpGetRequest.ContentType = "text/xml";

                using (httpGetResponse = httpGetRequest.GetResponse() as HttpWebResponse)
                {
                    StreamReader reader = new StreamReader(httpGetResponse.GetResponseStream());
                    using (var memstream = new MemoryStream())
                    {
                        reader.BaseStream.CopyTo(memstream);
                        announceResponse = memstream.ToArray();
                    }
                }
                if (httpGetResponse.StatusCode == HttpStatusCode.OK)
                {
                    constructResponse(announceResponse, ref response);
                }
                else
                {
                    response = new AnnounceResponse();
                    response.statusCode = (UInt32)httpGetResponse.StatusCode;
                    response.statusMessage = httpGetResponse.StatusDescription;

                }
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
                response.statusMessage = ex.Message;
            }

            return (response);

        }

        public void startAnnouncing()
        {
            try
            {
                _announceTimer = new System.Timers.Timer(_interval);
                _announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(sender, e, this);
                _announceTimer.AutoReset = true;
                _announceTimer.Enabled = true;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void stopAnnonncing()
        {
            try
            {
                _announceTimer.Stop();
                _announceTimer.Dispose();
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void update(AnnounceResponse response)
        {
            try
            {
                UInt32 oldInterval = _interval;
                if (response.minInterval != 0)
                {
                    _interval = response.minInterval;
                }
                else
                {
                    _interval = response.interval;
                }
                _trackerID = response.trackerID;
                if (oldInterval != _interval)
                {
                    stopAnnonncing();
                    startAnnouncing();
                }
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }
    }
}
