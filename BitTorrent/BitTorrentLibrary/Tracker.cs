//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide all the necessary functionality for communication 
// with remote trackers. This should support both UDP/HTTP requests but 
// at present only HTTP.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Timers;

namespace BitTorrent
{
    /// <summary>
    /// Tracker class.
    /// </summary>
    public class Tracker
    {

        /// <summary>
        /// Tracker Announce event types.
        /// </summary>
        public enum TrackerEvent
        {
            started = 0,    // The first request to the tracker must include the event key with this value
            stopped,        // Must be sent to the tracker if the client is shutting down gracefully.
            completed       // Must be sent to the tracker when the download completes
        };

        private System.Timers.Timer _announceTimer;         // Timer for sending tracker announce events
        private string _trackerURL = String.Empty;          // URL for tracker announce
        private string _peerID = String.Empty;              // Peers unique ID
        private FileAgent _torrentFileAgent;                // File Agent for downloads
        private AnnounceResponse _currentTrackerResponse;   // Last tracker announce response
        private UInt32 _port = 6681;                        // Port that client s listening on 
        private string _ip = String.Empty;                  // IP of host performing announce
        private UInt32 _compact = 1;                        // Is the returned peer list compressed (1=yes,0=no)
        private UInt32 _noPeerID;                           // Unique peer ID for downloader
        private UInt64 _uploaded;                           // Total uploaded bytes of torrent to remote clients
        private UInt64 _downloaded;                         // Total downloaed bytes of file to local client
        private UInt64 _left;                               // Bytes left in file to be downloaded
        private TrackerEvent _event;                        // Current state of torrent downloading
        private string _key = String.Empty;                 // An additional identification that is not shared with any other peers (optional)
        private string _trackerID = String.Empty;           // String that the client should send back on its next announcements. (optional).
        private UInt32 _numWanted = 5;                      // Number of required download clients
        private UInt32 _interval = 2000;                    // Polling interval between each announce

        public UInt64 Uploaded { get => _uploaded; set => _uploaded = value; }
        public UInt64 Downloaded { get => _downloaded; set => _downloaded = value; }
        public UInt64 Left { get => _left; set => _left = value; }
        public TrackerEvent Event { get => _event; set => _event = value; }

        /// <summary>
        /// Decodes the announce request response recieved from a tracker.
        /// </summary>
        /// <param name="announceResponse">Announce response.</param>
        /// <param name="decodedResponse">Response.</param>
        private void DecodeAnnounceResponse(byte[] announceResponse, ref AnnounceResponse decodedResponse)
        {

            BNodeBase decodedAnnounce = Bencoding.Decode(announceResponse);

            UInt32.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "complete"), out decodedResponse.complete);
            UInt32.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "incomplete"), out decodedResponse.incomplete);

            BNodeBase field = Bencoding.GetDictionaryEntry(decodedAnnounce, "peers");
            if (field != null)
            {
                decodedResponse.peers = new List<PeerDetails>();
                if (field is BNodeString)
                {
                    byte[] peers = ((BNodeString)field).str;
                    UInt32 numberPeers = (UInt32)peers.Length / 6;
                    for (var num = 0; num < peers.Length; num += 6)
                    {
                        PeerDetails peer = new PeerDetails();
                        peer._peerID = String.Empty;
                        peer.ip = $"{peers[num]}.{peers[num + 1]}.{peers[num + 2]}.{peers[num + 3]}";
                        if (peer.ip.Contains(":"))
                        {
                            peer.ip = peer.ip.Substring(peer.ip.LastIndexOf(":", StringComparison.Ordinal) + 1);
                        }
                        peer.port = (UInt32)peers[num + 4] * 256 + peers[num + 5];
                        if (peer.ip != _ip) // Ignore self in peers list
                        {
                            Log.Logger.Trace($"Peer {peer.ip} Port {peer.port} found.");
                            decodedResponse.peers.Add(peer);
                        }
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
                            BNodeBase peerField = Bencoding.GetDictionaryEntry(peerDictionaryItem, "ip");
                            if (peerField != null)
                            {
                                string path = string.Empty;
                                peer.ip = Encoding.ASCII.GetString(((BitTorrent.BNodeString)peerField).str);
                            }
                            if (peer.ip.Contains(":"))
                            {
                                peer.ip = peer.ip.Substring(peer.ip.LastIndexOf(":", StringComparison.Ordinal) + 1);
                            }
                            peerField = Bencoding.GetDictionaryEntry(peerDictionaryItem, "port");
                            if (peerField != null)
                            {
                                string path = string.Empty;
                                peer.port = UInt32.Parse(Encoding.ASCII.GetString(((BitTorrent.BNodeNumber)peerField).number));
                            }
                            if (peer.ip != _ip) // Ignore self in peers list
                            {
                                Log.Logger.Trace($"Peer {peer.ip} Port {peer.port} found.");
                                decodedResponse.peers.Add(peer);
                            }
                        }
                    }
                }

            }

            UInt32.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "interval"), out decodedResponse.interval);
            UInt32.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "min interval"), out decodedResponse.minInterval);
            decodedResponse.trackerID = Bencoding.GetDictionaryEntryString(decodedAnnounce, "tracker id");
            decodedResponse.statusMessage = Bencoding.GetDictionaryEntryString(decodedAnnounce, "failure reason");
            decodedResponse.statusMessage = Bencoding.GetDictionaryEntryString(decodedAnnounce, "warning message");

            decodedResponse.announceCount++;

        }

        /// <summary>
        /// Encodes infohash bytes into URL string.
        /// </summary>
        /// <returns>InfoHash encoded string.</returns>
        /// <param name="infoHashBytes">To encode.</param>
        private string EncodeInfoHashForURL(byte[] infoHashBytes)
        {
            return (Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(infoHashBytes, 0, infoHashBytes.Length)));

        }

        /// <summary>
        /// Encodes the tracker announce URL.
        /// </summary>
        /// <returns>The tracker URL.</returns>
        private string EncodeTrackerAnnounceURL()
        {
            string url = _trackerURL +
            "?info_hash=" + EncodeInfoHashForURL(_torrentFileAgent.TorrentMetaInfo.MetaInfoDict["info hash"]) +
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

        /// <summary>
        /// Updates all peers status.
        /// </summary>
        private void UpdatePeersStatus()
        {
            int unChokedPeers = 0;
            int activePeers = 0;

            foreach (var peer in _torrentFileAgent.RemotePeers)
            {
                if (peer.PeerChoking.WaitOne(0))
                {
                    unChokedPeers++;
                }
                if (peer.Active)
                {
                    activePeers++;
                }
            }

            Log.Logger.Info($"Unchoked Peers {unChokedPeers}/{ _torrentFileAgent.RemotePeers.Count}");
            Log.Logger.Info($"Active Peers {activePeers}/{ _torrentFileAgent.RemotePeers.Count}");

        }

        /// <summary>
        /// On  announce event send announce request to tracker and get response.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="e">E.</param>
        /// <param name="tracker">Tracker.</param>
        private static void OnAnnounceEvent(Object source, ElapsedEventArgs e, Tracker tracker)
        {
            tracker._currentTrackerResponse = tracker.Announce();
            tracker.UpdatePeersStatus();
        }

        /// <summary>
        /// Initialize Tracker.
        /// </summary>
        /// <param name="torrentFileAgent">Torrent file agent.</param>
        public Tracker(FileAgent torrentFileAgent)
        {
            _trackerURL = Encoding.ASCII.GetString(torrentFileAgent.TorrentMetaInfo.MetaInfoDict["announce"]);
            _torrentFileAgent = torrentFileAgent;
            _peerID = PeerID.get();
            _ip = Peer.GetLocalHostIP();

        }

        /// <summary>
        /// Perform an announce request to tracker and return any response.
        /// </summary>
        /// <returns>The response.</returns>
        public AnnounceResponse Announce()
        {

            Log.Logger.Info($"Announce: info_hash={EncodeInfoHashForURL(_torrentFileAgent.TorrentMetaInfo.MetaInfoDict["info hash"])} " +
                  $"peer_id={_peerID} port={_port} compact={_compact} no_peer_id={_noPeerID} uploaded={Uploaded}" +
                  $"downloaded={Downloaded} left={Left} event={Event} ip={_ip} key={_key} trackerid={_trackerID} numwanted={_numWanted}");

            AnnounceResponse response = new AnnounceResponse();

            try
            {

                HttpWebRequest httpGetRequest = WebRequest.Create(EncodeTrackerAnnounceURL()) as HttpWebRequest;

                httpGetRequest.Method = "GET";
                httpGetRequest.ContentType = "text/xml";

                using (HttpWebResponse httpGetResponse = httpGetRequest.GetResponse() as HttpWebResponse)
                {
                    byte[] announceResponse;
                    StreamReader reader = new StreamReader(httpGetResponse.GetResponseStream());
                    using (var memstream = new MemoryStream())
                    {
                        reader.BaseStream.CopyTo(memstream);
                        announceResponse = memstream.ToArray();
                    }
                    response.statusCode = (UInt32)httpGetResponse.StatusCode;
                    if (httpGetResponse.StatusCode == HttpStatusCode.OK)
                    {
                        DecodeAnnounceResponse(announceResponse, ref response);
                    }
                    else
                    {
                        response.statusMessage = httpGetResponse.StatusDescription;
                    }
                }

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex); 
                // Display first error as message concatination can be unreadable if too deep
                throw new Error("BitTorrent Error (Tracker): " + ex.GetBaseException().Message);
            }

            return (response);

        }

        /// <summary>
        /// Starts the announcing requests to tracker.
        /// </summary>
        public void StartAnnouncing()
        {
            try
            {
                _announceTimer = new System.Timers.Timer(_interval);
                _announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(sender, e, this);
                _announceTimer.AutoReset = true;
                _announceTimer.Enabled = true;
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Tracker): " + ex.Message);
            }
        }

        /// <summary>
        /// Stops the annonncing.
        /// </summary>
        public void StopAnnonncing()
        {
            try
            {
                if (_announceTimer != null)
                {
                    _announceTimer.Stop();
                    _announceTimer.Dispose();
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Tracker): " + ex.Message);
            }
        }

        /// <summary>
        /// Update the specified response.
        /// </summary>
        /// <param name="response">Response.</param>
        public void Update(AnnounceResponse response)
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
                    StopAnnonncing();
                    StartAnnouncing();
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Tracker): " + ex.Message);
            }
        }
    }
}
