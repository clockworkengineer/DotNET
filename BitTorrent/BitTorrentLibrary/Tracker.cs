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
    public class Tracker
    {
        /// <summary>
        /// Update swarm of active peers delegate
        /// </summary>
        public delegate void UpdatePeers(List<PeerDetails> peers);
        /// <summary>
        /// Tracker Announce event types.
        /// </summary>
        public static readonly string[] EventString = { "", "started", "stopped", "completed" };
        public enum TrackerEvent
        {
            None = 0,      // Default announce has none for event
            started = 1,   // The first request to the tracker must include the event key with this value
            stopped = 2,   // Must be sent to the tracker if the client is shutting down gracefully        
            completed = 3   // Must be sent to the tracker when the download completes
        };

        private readonly IAnnouncer _announcer;                 // Announcer for tracker
        protected Timer _announceTimer;                         // Timer for sending tracker announce events
        protected UpdatePeers _updatePeerSwarm;                 // Update peer swarm with connected peers
        public UInt64 Uploaded { get; set; }                    // Bytes left in file to be downloaded
        public UInt64 Downloaded { get; set; }                  // Total downloaed bytes of file to local client
        public UInt64 Left { get; set; }                        // Bytes left in file to be downloaded
        public TrackerEvent Event { get; set; }                 // Current state of torrent downloading
        public string PeerID { get; set; } = String.Empty;      // Peers unique ID
        public uint Port { get; set; } = 6681;                  // Port that client s listening on 
        public string Ip { get; set; } = String.Empty;          // IP of host performing announce
        public uint Compact { get; set; } = 1;                  // Is the returned peer list compressed (1=yes,0=no)
        public uint NoPeerID { get; set; }                      // Unique peer ID for downloader
        public string Key { get; set; } = String.Empty;         // An additional identification that is not shared with any other peers (optional)
        public string TrackerID { get; set; } = String.Empty;   // String that the client should send back on its next announcements. (optional).

        public uint NumWanted { get; set; } = 5;            // Number of required download clients
        public byte[] InfoHash { get; set; }                 // Encoded info hash for URI
        public string TrackerURL { get; set; } = String.Empty;  // Tracker URL
        public uint Interval { get; set; } = 2000;              // Polling interval between each announce

        public uint MinInterval { get; set; }                   // Minumum allowed polling interval 

        /// <summary>
        /// Perform announce request on timer tick
        /// </summary>
        /// <param name="tracker"></param>
        private static void OnAnnounceEvent(Tracker tracker)
        {
            AnnounceResponse response = tracker._announcer.Announce(tracker);
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
            PeerID = BitTorrentLibrary.PeerID.Get();
            Ip = Peer.GetLocalHostIP();
            InfoHash = infoHash;
            TrackerURL = trackerURL;
            _updatePeerSwarm = updatePeerSwarm;
            if (!TrackerURL.StartsWith("http://"))
            {
                if (TrackerURL.StartsWith("udp://"))
                {
                    _announcer = new AnnouncerUDP(TrackerURL);
                }
                else
                {
                    throw new Error("BitTorrent (Tracker) Error: Invalid tracker URL.");
                }
            }
            else
            {
                _announcer = new AnnouncerHTTP(TrackerURL);
            }
        }
        /// <summary>
        /// Restart announce on interval changing and save minimum interval and trackr ID.
        /// </summary>
        /// <param name="response"></param>
        private void UpdateRunningStatusFromAnnounce(AnnounceResponse response)
        {
            try
            {
                TrackerID = response.trackerID;
                MinInterval = response.minInterval;

                if (response.interval > MinInterval)
                {
                    UInt32 oldInterval = Interval;
                    Interval = response.interval;
                    if (oldInterval != Interval)
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
        /// On  announce event send announce request to tracker and get response.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="e">E.</param>
        /// <param name="tracker">Tracker.</param>
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
                _announceTimer = new System.Timers.Timer(Interval);
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