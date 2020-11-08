using System.Threading.Tasks;
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
    public delegate void TrackerCallBack(Object callBackData);            // Tracker callback
    public enum TrackerEvent
    {
        None = 0,      // Default announce has none for event
        started = 1,   // The first request to the tracker must include the event key with this value
        stopped = 2,   // Must be sent to the tracker if the client is shutting down gracefully        
        completed = 3   // Must be sent to the tracker when the download completes
    };
    public enum TrackerStatus
    {
        Running,    // Currently running
        Stopped,    // Stopped from annoucing to server
        Stalled     // Stalled because of an error
    }
    public class Tracker
    {
        /// <summary>
        /// Tracker Announce event types.
        /// </summary>
        public static readonly string[] EventString = { "", "started", "stopped", "completed" };
        private AnnounceResponse _currentRespone;               // Last announce response 
        private readonly TorrentContext _tc;                    // Torrent context
        private readonly IAnnouncer _announcer;                 // Announcer for tracker
        internal Timer _announceTimer;                          // Timer for sending tracker announce events
        internal AsyncQueue<PeerDetails> _peerSwarmQueue;       // Peers to add to swarm queue
        internal TrackerStatus _trackerStatus;                  // Current tracker status
        public TrackerEvent Event { get; set; }                 // Current state of torrent downloading
        public string PeerID { get; }                           // Peers unique ID
        public uint Port { get; } = Host.DefaultPort;           // Port that client s listening on 
        public string Ip { get; set; }                          // IP of host performing announce
        public uint Compact { get; } = 1;                       // Is the returned peer list compressed (1=yes,0=no)
        public uint NoPeerID { get; }                           // Unique peer ID for downloader
        public string Key { get; }                              // An additional identification that is not shared with any other peers (optional)
        public string TrackerID { get; set; }                   // String that the client should send back on its next announcements. (optional).
        public int NumWanted { get; set; } = 5;                 // Number of required download clients
        public byte[] InfoHash { get; }                         // Encoded info hash for URI
        public string TrackerURL { get; }                       // Tracker URL
        public uint Interval { get; set; } = 2000;              // Polling interval between each announce
        public uint MinInterval { get; set; }                   // Minumum allowed polling interval
        public TrackerCallBack CallBack { get; set; }           // Tracker ping callback function
        public Object CallBackData { get; set; }                // Tracker ping callback function data
        public UInt64 Downloaded => _tc.TotalBytesDownloaded;   // Total downloaded bytes of torrent to local client
        public UInt64 Left => _tc.BytesLeftToDownload();        // Bytes left in torrent to download
        public UInt64 Uploaded => _tc.TotalBytesUploaded;       // Total bytes uploaded

        /// <summary>
        /// Perform announce request on timer tick
        /// </summary>
        /// <param name="tracker"></param>
        private static void OnAnnounceEvent(Tracker tracker)
        {
            try
            {
                if (tracker._trackerStatus != TrackerStatus.Stalled)
                {
                    tracker._currentRespone = tracker._announcer.Announce(tracker);

                    if (!tracker._currentRespone.failure)
                    {
                        Log.Logger.Info("Queuing new peers for swarm ....");

                        if (tracker._tc.Status == TorrentStatus.Downloading)
                        {
                            foreach (var peerDetails in tracker._currentRespone.peers)
                            {
                                tracker._peerSwarmQueue?.Enqueue(peerDetails);
                            }
                            tracker.NumWanted = Math.Max(tracker._tc.MaximumSwarmSize - tracker._tc.PeerSwarm.Count, 0);

                        }
                        tracker.UpdateRunningStatusFromAnnounce(tracker._currentRespone);
                    }
                    else
                    {
                        throw new Exception("Remote tracker failure: " + tracker._currentRespone.statusMessage);
                    }
                }

            }
            catch (Exception ex)
            {
                tracker._trackerStatus = TrackerStatus.Stalled;
                Log.Logger.Debug("BitTorrent (Tracker) Error : " + ex.Message);
                if (!tracker._currentRespone.failure)
                {
                    tracker._currentRespone.failure = true;
                    tracker._currentRespone.statusMessage = ex.Message;
                }
            }
            tracker._announceTimer?.Start();
            tracker.CallBack?.Invoke(tracker.CallBackData);

        }
        /// <summary>
        /// Restart announce on interval changing and save minimum interval and tracker ID.
        /// </summary>
        /// <param name="response"></param>
        private void UpdateRunningStatusFromAnnounce(AnnounceResponse response)
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
        /// <summary>
        /// Initialise BitTorrent Tracker.
        /// </summary>
        /// <param name="tc"></param>
        public Tracker(TorrentContext tc)
        {
            _trackerStatus = TrackerStatus.Stopped;
            PeerID = BitTorrentLibrary.PeerID.Get();
            Ip = Host.GetIP();
            InfoHash = tc.InfoHash;
            TrackerURL = tc.TrackerURL;
            _tc = tc;
            _tc.MainTracker = this;

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
        /// Change tracker status.
        /// </summary>
        /// <param name="status"></param>
        internal void ChangeStatus(TrackerEvent status)
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
                //  Swarm queue needs to be initialised
                if (_peerSwarmQueue == null)
                {
                    throw new Exception("Peer swarm queue has not been set.");
                }

                // If all of torrent downloaded reset total bytes downloaded
                if (Left == 0)
                {
                    _tc.TotalBytesDownloaded = 0;
                    _tc.TotalBytesToDownload = 0;
                }

                ChangeStatus(TrackerEvent.started);
                if (_currentRespone.failure)
                {
                    throw new Exception("Tracker failure: " + _currentRespone.statusMessage);
                }

                _announceTimer = new System.Timers.Timer(Interval);
                _announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(this);
                _announceTimer.AutoReset = false;
                _announceTimer.Enabled = true;

                _trackerStatus = TrackerStatus.Running;
                _announceTimer?.Start();
                CallBack?.Invoke(CallBackData);

            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                _trackerStatus = TrackerStatus.Stalled;
                CallBack?.Invoke(CallBackData);
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
                    _trackerStatus = TrackerStatus.Stopped;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                _trackerStatus = TrackerStatus.Stalled;
                throw new Error("BitTorrent (Tracker) Error: " + ex.Message);
            }
        }
        /// <summary>
        /// ASync StartAnnouncing().
        /// </summary>
        /// <returns></returns>
        public async Task StartAnnouncingAsync()
        {
            await Task.Run(() => StartAnnouncing()).ConfigureAwait(false);
        }
        /// <summary>
        /// Restart a stalled tracker. 
        /// </summary>
        public void RestartAnnouncing()
        {
            StopAnnouncing();
            StartAnnouncing();
        }

    }
}