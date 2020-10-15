//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide all the necessary functionality for communication 
// with remote trackers using HTTP/UDP. At present this just uses the main
// announce tracker found and doesnt use the announce-list backups.
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;
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

        private readonly DownloadContext _dc;                   // Download context
        private readonly List<Exception> _announcerExceptions;  // Exceptions raised during any announces
        private readonly IAnnouncer _announcer;                 // Announcer for tracker
        protected Timer _announceTimer;                         // Timer for sending tracker announce events
        protected UpdatePeers _updatePeerSwarm;                 // Update peer swarm with connected peers
        public UInt64 Uploaded { get; set; }                    // Bytes left in file to be downloaded
        public TrackerEvent Event { get; set; }                 // Current state of torrent downloading
        public string PeerID { get; set; } = String.Empty;      // Peers unique ID
        public uint Port { get; set; } = Host.DefaultPort;      // Port that client s listening on 
        public string Ip { get; set; } = String.Empty;          // IP of host performing announce
        public uint Compact { get; set; } = 1;                  // Is the returned peer list compressed (1=yes,0=no)
        public uint NoPeerID { get; set; }                      // Unique peer ID for downloader
        public string Key { get; set; } = String.Empty;         // An additional identification that is not shared with any other peers (optional)
        public string TrackerID { get; set; } = String.Empty;   // String that the client should send back on its next announcements. (optional).
        public int NumWanted { get; set; } = 5;                // Number of required download clients
        public byte[] InfoHash { get; set; }                    // Encoded info hash for URI
        public string TrackerURL { get; set; } = String.Empty;  // Tracker URL
        public uint Interval { get; set; } = 2000;              // Polling interval between each announce
        public uint MinInterval { get; set; }                   // Minumum allowed polling interval
        public int MaximumSwarmSize { get; set; }              // Maximim swarm size
        public UInt64 Downloaded => _dc.TotalBytesDownloaded;   // Total downloaded bytes of torrent to local client
        public UInt64 Left => _dc.BytesLeftToDownload();        // Bytes left in torrent to download


        /// <summary>
        /// Perform announce request on timer tick
        /// </summary>
        /// <param name="tracker"></param>
        private static void OnAnnounceEvent(Tracker tracker)
        {
            try
            {
                AnnounceResponse response = tracker._announcer.Announce(tracker);
                tracker._updatePeerSwarm?.Invoke(response.peers);
                tracker.UpdateRunningStatusFromAnnounce(response);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                tracker._announcerExceptions.Add(ex);
            }
            finally
            {
                tracker._announceTimer?.Start();
            }
        }
        /// <summary>
        /// Initialise BitTorrent Tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        /// <param name="infoHash"></param>
        /// <param name="updatePeerSwarm"></param>
        public Tracker(Agent agent, Downloader downloader, int maximumSwarmSize = 10)
        {
            PeerID = BitTorrentLibrary.PeerID.Get();
            Ip = Host.GetIP();
            InfoHash = agent.InfoHash;
            TrackerURL = agent.TrackerURL;
            MaximumSwarmSize = maximumSwarmSize;
            _updatePeerSwarm = agent.UpdatePeerSwarm;
            _dc = downloader.Dc;
            agent.MainTracker = this;
            _announcerExceptions = new List<Exception>();

            if (!TrackerURL.StartsWith("http://"))
            {
                if (TrackerURL.StartsWith("udp://"))
                {
                    Log.Logger.Info("Main tracker is UDP...");
                    _announcer = new AnnouncerUDP(TrackerURL);
                }
                else
                {
                    throw new Error("BitTorrent (Tracker) Error: Invalid tracker URL.");
                }
            }
            else
            {
                Log.Logger.Info("Main tracker is HTTP...");
                _announcer = new AnnouncerHTTP(TrackerURL);
            }
        }
        /// <summary>
        /// Restart announce on interval changing and save minimum interval and trackre ID.
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
                        if (_announceTimer != null)
                        {
                            _announceTimer.Stop();
                            _announceTimer.Interval = Interval;
                            _announceTimer.Start();
                        }
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
        /// Change tracker event status and send to server.
        /// </summary>
        /// <param name="tracker">Tracker.</param>
        /// <summary>
        /// Change tracker status.
        /// </summary>
        public void ChangeStatus(TrackerEvent status)
        {
            try
            {
                _announceTimer?.Stop();
                Event = status;
                OnAnnounceEvent(this);
                Event = TrackerEvent.None;  // Reset it back to default on next tick
                _announceTimer?.Start();
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
        /// Starts the announce requests to tracker.
        /// </summary>
        public void StartAnnouncing()
        {
            try
            {
                // If all of torrent downloaded reset total bytes downloaded
                if (Left==0) {
                    _dc.TotalBytesDownloaded = 0;
                    _dc.TotalBytesToDownload = 0;
                }
                ChangeStatus(TrackerEvent.started);
                _announceTimer = new System.Timers.Timer(Interval);
                _announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(this);
                _announceTimer.AutoReset = false;
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