//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide all the necessary functionality for communication 
// with remote trackers using HTTP/UDP.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Timers;

namespace BitTorrentLibrary
{
    /// <summary>
    /// Tracker class.
    /// </summary>
    public class TrackerHTTP
    {
        /// <summary>
        /// Update swarm of active peers delegate
        /// </summary>
        public delegate void UpdatePeers(List<PeerDetails> peers);
        /// <summary>
        /// Tracker Announce event types.
        /// </summary>
        private static readonly string[] EventString = { "", "started", "stopped", "completed" };
        public enum TrackerEvent
        {
            None = 0,      // Default announce has none for event
            started = 1,   // The first request to the tracker must include the event key with this value
            stopped = 2,   // Must be sent to the tracker if the client is shutting down gracefully        
            completed = 3   // Must be sent to the tracker when the download completes
        };
        private Timer _announceTimer;                       // Timer for sending tracker announce events
        private readonly string _peerID = String.Empty;     // Peers unique ID
        private readonly UInt32 _port = 6681;               // Port that client s listening on 
        private readonly string _ip = String.Empty;         // IP of host performing announce
        private readonly UInt32 _compact = 1;               // Is the returned peer list compressed (1=yes,0=no)
        private readonly UInt32 _noPeerID;                  // Unique peer ID for downloader
        private readonly string _key = String.Empty;        // An additional identification that is not shared with any other peers (optional)
        private string _trackerID = String.Empty;           // String that the client should send back on its next announcements. (optional).
        private readonly UInt32 _numWanted = 5;             // Number of required download clients
        private readonly string _infoHash = String.Empty;   // Encoded info hash for URI
        private readonly string _trackerURL = String.Empty; // Tracker URL
        private UInt32 _interval = 2000;                    // Polling interval between each announce
        private UInt32 _minInterval;                        // Minumum allowed polling interval 
        private readonly UpdatePeers _updatePeerSwarm;      // Update peer swarm with connected peers
        public UInt64 Uploaded { get; set; }                // Bytes left in file to be downloaded
        public UInt64 Downloaded { get; set; }              // Total downloaed bytes of file to local client
        public UInt64 Left { get; set; }                    // Bytes left in file to be downloaded
        public TrackerEvent Event { get; set; }             // Current state of torrent downloading

        /// <summary>
        /// Decodes the announce request response recieved from a tracker.
        /// </summary>
        /// <param name="announceResponse">Announce response.</param>
        /// <param name="decodedResponse">Response.</param>
        private void DecodeAnnounceResponse(byte[] announceResponse, ref AnnounceResponse decodedResponse)
        {
            BNodeBase decodedAnnounce = Bencoding.Decode(announceResponse);

            decodedResponse.statusMessage = Bencoding.GetDictionaryEntryString(decodedAnnounce, "failure reason");
            if (decodedResponse.statusMessage != "")
            {
                decodedResponse.failure = true;
                return; // If failure present then ignore rest of reply.
            }

            UInt32.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "complete"), out decodedResponse.complete);
            UInt32.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "incomplete"), out decodedResponse.incomplete);

            BNodeBase field = Bencoding.GetDictionaryEntry(decodedAnnounce, "peers");
            if (field != null)
            {
                decodedResponse.peers = new List<PeerDetails>();
                if (field is BNodeString bNodeString)
                {
                    byte[] peers = (bNodeString).str;
                    for (var num = 0; num < peers.Length; num += 6)
                    {
                        PeerDetails peer = new PeerDetails
                        {
                            _peerID = String.Empty,
                            ip = $"{peers[num]}.{peers[num + 1]}.{peers[num + 2]}.{peers[num + 3]}"
                        };
                        // if (peer.ip.Contains(":"))
                        // {
                        //     peer.ip = peer.ip.Substring(peer.ip.LastIndexOf(":", StringComparison.Ordinal) + 1);
                        // }
                        peer.port = ((UInt32)peers[num + 4] * 256) + peers[num + 5];
                        if (peer.ip != _ip) // Ignore self in peers list
                        {
                            Log.Logger.Trace($"Peer {peer.ip} Port {peer.port} found.");
                            decodedResponse.peers.Add(peer);
                        }
                    }
                }
                else if (field is BNodeList bNodeList)
                {
                    foreach (var listItem in (bNodeList).list)
                    {
                        if (listItem is BNodeDictionary bNodeDictionary)
                        {
                            PeerDetails peer = new PeerDetails();
                            BNodeBase peerDictionaryItem = (bNodeDictionary);
                            BNodeBase peerField = Bencoding.GetDictionaryEntry(peerDictionaryItem, "ip");
                            if (peerField != null)
                            {
                                peer.ip = Encoding.ASCII.GetString(((BitTorrentLibrary.BNodeString)peerField).str);
                            }
                            if (peer.ip.Contains(":"))
                            {
                                peer.ip = peer.ip.Substring(peer.ip.LastIndexOf(":", StringComparison.Ordinal) + 1);
                            }
                            peerField = Bencoding.GetDictionaryEntry(peerDictionaryItem, "port");
                            if (peerField != null)
                            {
                                peer.port = UInt32.Parse(Encoding.ASCII.GetString(((BitTorrentLibrary.BNodeNumber)peerField).number));
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

            decodedResponse.statusMessage = Bencoding.GetDictionaryEntryString(decodedAnnounce, "warning message");

            decodedResponse.announceCount++;
        }

        /// <summary>
        /// Restart announce on interval changing and save minimum interval and trackr ID.
        /// </summary>
        /// <param name="response"></param>
        private void UpdateRunningStatusFromAnnounce(AnnounceResponse response)
        {
            try
            {
                _trackerID = response.trackerID;
                _minInterval = response.minInterval;

                if (response.interval > _minInterval)
                {
                    UInt32 oldInterval = _interval;
                    _interval = response.interval;
                    if (oldInterval != _interval)
                    {
                        StopAnnouncing();
                        StartAnnouncing();
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
                throw new Error("BitTorrent Error (Tracker): " + ex.Message);
            }
        }
        /// <summary>
        /// Perform an announce request to tracker and return any response.
        /// </summary>
        /// <returns>The response.</returns>
        private AnnounceResponse Announce()
        {
            Log.Logger.Info($"Announce: info_hash={_infoHash} " +
                  $"peer_id={_peerID} port={_port} compact={_compact} no_peer_id={_noPeerID} uploaded={Uploaded}" +
                  $"downloaded={Downloaded} left={Left} event={EventString[(int)Event]} ip={_ip} key={_key} trackerid={_trackerID} numwanted={_numWanted}");

            AnnounceResponse response = new AnnounceResponse();

            try
            {
                string announceURL = $"{_trackerURL}?info_hash={_infoHash}&peer_id={_peerID}&port={_port}&compact={_compact}&no_peer_id={_noPeerID}&uploaded={Uploaded}&downloaded={Downloaded}&left={Left}&event={EventString[(int)Event]}&ip={_ip}&key={_key}&trackerid={_trackerID}&numwanted={_numWanted}";

                HttpWebRequest httpGetRequest = WebRequest.Create(announceURL) as HttpWebRequest;

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
                    if (httpGetResponse.StatusCode == HttpStatusCode.OK)
                    {
                        DecodeAnnounceResponse(announceResponse, ref response);
                    }
                    else
                    {
                        Log.Logger.Debug("Bittorrent (Tracker) Error: " + httpGetResponse.StatusDescription);
                        throw new Error("Bittorrent (Tracker) Error: " + httpGetResponse.StatusDescription);
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

            return response;
        }
        /// <summary>
        /// On  announce event send announce request to tracker and get response.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="e">E.</param>
        /// <param name="tracker">Tracker.</param>
        private static void OnAnnounceEvent(TrackerHTTP tracker)
        {
            AnnounceResponse response = tracker.Announce();
            tracker._updatePeerSwarm?.Invoke(response.peers);
            tracker.UpdateRunningStatusFromAnnounce(response);
        }
        /// <summary>
        /// Is a specified tracker supported.
        /// </summary>
        /// <param name="trackerURL"></param>
        /// <returns>==true tracker supported</returns>
        public bool IsSupported(string trackerURL)
        {
            return trackerURL.StartsWith("http://");
        }
        /// <summary>
        /// Initialise BitTorrent Tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        /// <param name="infoHash"></param>
        /// <param name="updatePeerSwarm"></param>
        public TrackerHTTP(string trackerURL, byte[] infoHash, UpdatePeers updatePeerSwarm = null)
        {
            _peerID = PeerID.Get();
            _ip = Peer.GetLocalHostIP();
            _infoHash = Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(infoHash, 0, infoHash.Length));
            _trackerURL = trackerURL;
            _updatePeerSwarm = updatePeerSwarm;
        }
        /// <summary>
        /// Change tracker status.
        /// </summary>
        public void ChangeStatus(TrackerEvent status)
        {
            _announceTimer?.Stop();
            Event = status;
            OnAnnounceEvent(this);
            Event = TrackerEvent.None;  // Reset it back to default on next tick
            _announceTimer?.Start();
        }
        /// <summary>
        /// Starts the announce requests to tracker.
        /// </summary>
        public void StartAnnouncing()
        {
            try
            {
                if (_announceTimer != null)
                {
                    StopAnnouncing();
                }
                _announceTimer = new System.Timers.Timer(_interval);
                _announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(this);
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
        /// Stop announcing to tracker..
        /// </summary>
        public void StopAnnouncing()
        {
            try
            {
                if (_announceTimer != null)
                {
                    _announceTimer.Stop();
                    _announceTimer.Dispose();
                    _announceTimer = null;
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
