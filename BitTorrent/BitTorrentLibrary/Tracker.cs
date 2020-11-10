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
using System.Timers;
using System.Threading.Tasks;
using System.Net;
using System.Text;


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
        internal Timer announceTimer;                           // Timer for sending tracker announce events
        internal AsyncQueue<PeerDetails> peerSwarmQueue;        // Peers to add to swarm queue
        internal TrackerStatus trackerStatus;                   // Current tracker status
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
        /// Log announce details.
        /// </summary>
        /// <param name="tracker"></param>
        internal static void LogAnnouce(Tracker tracker)
        {
            Log.Logger.Info($"Announce: info_hash={Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(tracker.InfoHash, 0, tracker.InfoHash.Length))} " +
                     $"peer_id={tracker.PeerID} port={tracker.Port} compact={tracker.Compact} no_peer_id={tracker.NoPeerID} uploaded={tracker.Uploaded}" +
                     $"downloaded={tracker.Downloaded} left={tracker.Left} event={Tracker.EventString[(int)tracker.Event]} ip={tracker.Ip} key={tracker.Key}" +
                     $" trackerid={tracker.TrackerID} numwanted={tracker.NumWanted}");

        }
        /// <summary>
        /// Perform announce request on timer tick
        /// </summary>
        /// <param name="tracker"></param>
        private void OnAnnounceEvent(Tracker tracker)
        {
            try
            {
                if (tracker.trackerStatus != TrackerStatus.Stalled)
                {
                    tracker._currentRespone = tracker._announcer.Announce(tracker);

                    if (!tracker._currentRespone.failure)
                    {
                        Log.Logger.Info("Queuing new peers for swarm ....");

                        if (tracker._tc.Status == TorrentStatus.Downloading)
                        {
                            foreach (var peerDetails in tracker._currentRespone.peers)
                            {
                                tracker.peerSwarmQueue?.Enqueue(peerDetails);
                            }
                            tracker.NumWanted = Math.Max(tracker._tc.maximumSwarmSize - tracker._tc.peerSwarm.Count, 0);

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
                tracker.trackerStatus = TrackerStatus.Stalled;
                Log.Logger.Debug("BitTorrent (Tracker) Error : " + ex.Message);
                if (!tracker._currentRespone.failure)
                {
                    tracker._currentRespone.failure = true;
                    tracker._currentRespone.statusMessage = ex.Message;
                }
            }
            tracker.announceTimer?.Start();
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

            if (_tc.Status == TorrentStatus.Downloading)
            {
                if (response.interval > MinInterval)
                {
                    UInt32 oldInterval = Interval;
                    Interval = response.interval;
                    if (oldInterval != Interval)
                    {
                        if (announceTimer != null)
                        {
                            announceTimer.Stop();
                            announceTimer.Interval = Interval;
                            announceTimer.Start();
                        }
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
            trackerStatus = TrackerStatus.Stopped;
            PeerID = BitTorrentLibrary.PeerID.Get();
            Ip = Host.GetIP();
            InfoHash = tc.infoHash;
            TrackerURL = tc.trackerURL;
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
            announceTimer?.Stop();
            Event = status;
            OnAnnounceEvent(this);
            Event = TrackerEvent.None;  // Reset it back to default on next tick
            announceTimer?.Start();
        }
        /// <summary>
        /// Starts the announce requests to tracker.
        /// </summary>
        public void StartAnnouncing()
        {
            try
            {
                //  Swarm queue needs to be initialised
                if (peerSwarmQueue == null)
                {
                    throw new Exception("Peer swarm queue has not been set.");
                }

                // If all of torrent downloaded reset total bytes downloaded
                if (Left == 0)
                {
                    _tc.TotalBytesDownloaded = 0;
                    _tc.TotalBytesToDownload = 0;
                    ChangeStatus(TrackerEvent.None);
                }
                else
                {
                    ChangeStatus(TrackerEvent.started);
                }

                if (_currentRespone.failure)
                {
                    throw new Exception("Tracker failure: " + _currentRespone.statusMessage);
                }

                announceTimer = new System.Timers.Timer(Interval);
                announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(this);
                announceTimer.AutoReset = false;
                announceTimer.Enabled = true;

                trackerStatus = TrackerStatus.Running;
                announceTimer?.Start();
                CallBack?.Invoke(CallBackData);
                _tc.trackerStarted.Set();

            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                trackerStatus = TrackerStatus.Stalled;
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
                if (announceTimer != null)
                {
                    announceTimer.Stop();
                    announceTimer.Dispose();
                    announceTimer = null;
                    trackerStatus = TrackerStatus.Stopped;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                trackerStatus = TrackerStatus.Stalled;
                throw new Error("BitTorrent (Tracker) Error: " + ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void StartAnnouncingAsync()
        {
            Task.Run(() => StartAnnouncing());
        }
        /// <summary>
        /// Restart a stalled tracker. 
        /// </summary>
        public void RestartAnnouncing()
        {
            StopAnnouncing();
            StartAnnouncing();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="seedingInerval"></param>
        public void SetSeedingInterval(UInt32 seedingInerval)
        {
            if (_tc.Status == TorrentStatus.Seeding)
            {
                if (seedingInerval > MinInterval)
                {

                    if (announceTimer != null)
                    {
                        announceTimer.Stop();
                        announceTimer.Interval = seedingInerval;
                        announceTimer.Start();
                    }

                }
            }
        }

    }
}