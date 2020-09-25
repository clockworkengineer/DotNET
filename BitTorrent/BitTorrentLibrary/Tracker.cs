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
    public interface ITracker
    {
        ulong Uploaded { get; set; }
        ulong Downloaded { get; set; }
        ulong Left { get; set; }
        Tracker.TrackerEvent Event { get; set; }
    }

    public class Tracker : ITracker
    {
        /// <summary>
        /// Update swarm of active peers delegate
        /// </summary>
        public delegate void UpdatePeers(List<PeerDetails> peers);
        /// <summary>
        /// Tracker Announce event types.
        /// </summary>
        protected static readonly string[] EventString = { "", "started", "stopped", "completed" };
        public enum TrackerEvent
        {
            None = 0,      // Default announce has none for event
            started = 1,   // The first request to the tracker must include the event key with this value
            stopped = 2,   // Must be sent to the tracker if the client is shutting down gracefully        
            completed = 3   // Must be sent to the tracker when the download completes
        };

        protected Timer _announceTimer;              // Timer for sending tracker announce events
        protected string _peerID = String.Empty;     // Peers unique ID
        protected UInt32 _port = 6681;               // Port that client s listening on 
        protected string _ip = String.Empty;         // IP of host performing announce
        protected UInt32 _compact = 1;               // Is the returned peer list compressed (1=yes,0=no)
        protected UInt32 _noPeerID;                  // Unique peer ID for downloader
        protected string _key = String.Empty;        // An additional identification that is not shared with any other peers (optional)
        protected string _trackerID = String.Empty;  // String that the client should send back on its next announcements. (optional).
        protected UInt32 _numWanted = 5;             // Number of required download clients
        protected byte[] _infoHash;                  // Encoded info hash for URI
        protected string _trackerURL = String.Empty; // Tracker URL
        protected UInt32 _interval = 2000;           // Polling interval between each announce
        protected UInt32 _minInterval;               // Minumum allowed polling interval 
        protected UpdatePeers _updatePeerSwarm;      // Update peer swarm with connected peers
        public UInt64 Uploaded { get; set; }         // Bytes left in file to be downloaded
        public UInt64 Downloaded { get; set; }       // Total downloaed bytes of file to local client
        public UInt64 Left { get; set; }             // Bytes left in file to be downloaded
        public TrackerEvent Event { get; set; }      // Current state of torrent downloading

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
            _infoHash = infoHash;// Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(infoHash, 0, infoHash.Length));
            _trackerURL = trackerURL;
            _updatePeerSwarm = updatePeerSwarm;
        }
    }
}