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
        ///
        public delegate void UpdatePeers(List<PeerDetails> peers); // Update swarm of active peers
        public enum TrackerEvent
        {
            started = 0,    // The first request to the tracker must include the event key with this value
            stopped,        // Must be sent to the tracker if the client is shutting down gracefully.
            completed       // Must be sent to the tracker when the download completes
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
        private readonly string _trackURL = String.Empty;   // Tracker URL
        private UInt32 _interval = 2000;                    // Polling interval between each announce
        private readonly UpdatePeers _updatePeerSwarm;
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
                        if (peer.ip.Contains(":"))
                        {
                            peer.ip = peer.ip.Substring(peer.ip.LastIndexOf(":", StringComparison.Ordinal) + 1);
                        }
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
                                peer.ip = Encoding.ASCII.GetString(((BitTorrent.BNodeString)peerField).str);
                            }
                            if (peer.ip.Contains(":"))
                            {
                                peer.ip = peer.ip.Substring(peer.ip.LastIndexOf(":", StringComparison.Ordinal) + 1);
                            }
                            peerField = Bencoding.GetDictionaryEntry(peerDictionaryItem, "port");
                            if (peerField != null)
                            {
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

            decodedResponse.statusMessage = Bencoding.GetDictionaryEntryString(decodedAnnounce, "warning message");

            decodedResponse.announceCount++;
        }

        /// <summary>
        /// Restart announce on interval changing.
        /// </summary>
        /// <param name="response"></param>
        private void UpdateRunningStatusFromAnnounce(AnnounceResponse response)
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

        /// <summary>
        /// On  announce event send announce request to tracker and get response.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="e">E.</param>
        /// <param name="tracker">Tracker.</param>
        private static void OnAnnounceEvent(Tracker tracker)
        {
            AnnounceResponse response = tracker.Announce();
            tracker._updatePeerSwarm?.Invoke(response.peers);
            tracker.UpdateRunningStatusFromAnnounce(response);
        }

        /// <summary>
        /// Initialise BitTorrent Tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        /// <param name="infoHash"></param>
        /// <param name="updatePeerSwarm"></param>
        public Tracker(string trackerURL, byte[] infoHash, UpdatePeers updatePeerSwarm)
        {
            _peerID = PeerID.Get();
            _ip = Peer.GetLocalHostIP();
            _infoHash = Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(infoHash, 0, infoHash.Length));
            _trackURL = trackerURL;
            _updatePeerSwarm = updatePeerSwarm;
        }

        /// <summary>
        /// Perform an announce request to tracker and return any response.
        /// </summary>
        /// <returns>The response.</returns>
        public AnnounceResponse Announce()
        {
            Log.Logger.Info($"Announce: info_hash={_infoHash} " +
                  $"peer_id={_peerID} port={_port} compact={_compact} no_peer_id={_noPeerID} uploaded={Uploaded}" +
                  $"downloaded={Downloaded} left={Left} event={Event} ip={_ip} key={_key} trackerid={_trackerID} numwanted={_numWanted}");

            AnnounceResponse response = new AnnounceResponse();

            try
            {
                string announceURL = $"{_trackURL}?info_hash={_infoHash}&peer_id={_peerID}&port={_port}&compact={_compact}&no_peer_id={_noPeerID}&uploaded={Uploaded}&downloaded={Downloaded}&left={Left}&event={Event}&ip={_ip}&key={_key}&trackerid={_trackerID}&numwanted={_numWanted}";

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
        /// Starts the announcing requests to tracker.
        /// </summary>
        public void StartAnnouncing()
        {
            try
            {
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
    }
}
