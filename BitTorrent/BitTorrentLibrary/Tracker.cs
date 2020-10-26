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
using System.Collections.Concurrent;
using System.Timers;

namespace BitTorrentLibrary
{
    public delegate void TrackerCallBack(Object callBackData);            // Tracker callback

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
        private TrackerCallBack _callBack;                      // Tracker ping callback function
        private Object _callBackData;                           // Tracker ping callback function data
        protected Timer _announceTimer;                         // Timer for sending tracker announce events
        private BlockingCollection<PeerDetails> _peerSwarmQueue;// Peers to add to swarm queue
        public TrackerEvent Event { get; set; }                 // Current state of torrent downloading
        public string PeerID { get; }                           // Peers unique ID
        public uint Port { get; } = Host.DefaultPort;           // Port that client s listening on 
        public string Ip { get; set; }                          // IP of host performing announce
        public uint Compact { get;  } = 1;                      // Is the returned peer list compressed (1=yes,0=no)
        public uint NoPeerID { get;  }                          // Unique peer ID for downloader
        public string Key { get;  }                             // An additional identification that is not shared with any other peers (optional)
        public string TrackerID { get; set; }                   // String that the client should send back on its next announcements. (optional).
        public int NumWanted { get; set; } = 5;                 // Number of required download clients
        public byte[] InfoHash { get; }                         // Encoded info hash for URI
        public string TrackerURL { get;  }                      // Tracker URL
        public uint Interval { get; set; } = 2000;              // Polling interval between each announce
        public uint MinInterval { get; set; }                   // Minumum allowed polling interval
        public UInt64 Downloaded => _dc.TotalBytesDownloaded;   // Total downloaded bytes of torrent to local client
        public UInt64 Left => _dc.BytesLeftToDownload();        // Bytes left in torrent to download
        public UInt64 Uploaded => _dc.TotalBytesUploaded;       // Total bytes uploaded

        /// <summary>
        /// Perform announce request on timer tick
        /// </summary>
        /// <param name="tracker"></param>
        private static void OnAnnounceEvent(Tracker tracker)
        {
            try
            {
                AnnounceResponse response = tracker._announcer.Announce(tracker);

                Log.Logger.Info("Queuing new peers for swarm ....");

                foreach (var peerDetails in response.peers)
                {
                    tracker._peerSwarmQueue?.Add(peerDetails);
                }
                tracker.NumWanted = Math.Max(tracker._dc.MaximumSwarmSize - tracker._dc.PeerSwarm.Count, 0);

                tracker._callBack?.Invoke(tracker._callBackData);
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
        /// Restart announce on interval changing and save minimum interval and tracker ID.
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
                throw new Error("BitTorrent (Tracker) Error: " + ex.Message);
            }
        }
        /// <summary>
        /// Initialise BitTorrent Tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        /// <param name="infoHash"></param>
        /// <param name="updatePeerSwarm"></param>
        public Tracker(DownloadContext dc)
        {
            PeerID = BitTorrentLibrary.PeerID.Get();
            Ip = Host.GetIP();
            InfoHash = dc.InfoHash;
            TrackerURL = dc.TrackerURL;
            _dc = dc;
            _dc.MainTracker = this;
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
        /// 
        /// </summary>
        /// <param name="peerSwarmQueue"></param>
        public void SetPeerSwarmQueue(BlockingCollection<PeerDetails> peerSwarmQueue) {
            _peerSwarmQueue = peerSwarmQueue;
        } 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="callBack"></param>
        /// <param name="callBackData"></param>
        public void SetTrackerCallBack(TrackerCallBack callBack, Object callBackData)
        {
            _callBack = callBack;
            _callBackData = callBackData;
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
                throw new Error("BitTorrent (Tracker) Error: " + ex.Message);
            }
        }
        /// <summary>
        /// Starts the announce requests to tracker.
        /// </summary>
        public void StartAnnouncing()
        {
            try
            {
                //  Swarm queue needs to be initialised
                if (_peerSwarmQueue == null)
                {
                    throw new Error("BitTorrent (Tracker) Error: Peer swarm queue has not been set.");
                }

                // If all of torrent downloaded reset total bytes downloaded
                if (Left == 0)
                {
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
                throw new Error("BitTorrent (Tracker) Error: " + ex.Message);
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
                throw new Error("BitTorrent (Tracker) Error: " + ex.Message);
            }
        }
    }
}