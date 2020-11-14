using System.Net.Mime;

//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Store and manage torrent contexts. It enables for them
// to be retrieved just using the infohash of a torrent. In combination with
// a peer ip it enables a Peer structure to be retrieved as well.
//
// Copyright 2020.
//

using System;
using System.Timers;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace BitTorrentLibrary
{
    public class Manager
    {
        private readonly ConcurrentDictionary<string, TorrentContext> _torrents; // Torrents downloading
        private readonly HashSet<string> _deadPeers;                             // Dead peers list
        private readonly UInt32 _deadPeerPurgeTimeOut;                           // Time minutes to perform dead peer purge
        private readonly Timer _deadPeerPurgeTimer;                              // Dead peer purge timer
        internal Int32 DeadPeerCount => _deadPeers.Count;                        // Number of dead 
        internal ICollection<TorrentContext> TorrentList => _torrents.Values;    // List of torrent contexts

        /// <summary>
        /// Purge dead peers.
        /// </summary>
        /// <param name="manager"></param>
        private void OnPurgeDeadTimerEvent(Manager manager)
        {
            Log.Logger.Info("(Manager) PURGING DEAD PEERS LIST.");
            manager._deadPeers.Clear();
        }
        /// <summary>
        /// Setup data and resources used by manager.
        /// </summary>
        public Manager(UInt32 deadPeerPurgeTimeOut = 15)
        {
            _torrents = new ConcurrentDictionary<string, TorrentContext>();
            _deadPeers = new HashSet<string>();
            _deadPeerPurgeTimeOut = deadPeerPurgeTimeOut;
            _deadPeerPurgeTimer = new System.Timers.Timer(_deadPeerPurgeTimeOut * 60 * 1000);
            _deadPeerPurgeTimer.Elapsed += (sender, e) => OnPurgeDeadTimerEvent(this);
            _deadPeerPurgeTimer.AutoReset = true;
            _deadPeerPurgeTimer.Enabled = true;
        }
        /// <summary>
        /// Retrieve torrent context for infohash.
        /// </summary>
        /// <param name="infohash"></param>
        /// <returns></returns>
        internal bool GetTorrentContext(byte[] infohash, out TorrentContext tc)
        {
            if (_torrents.TryGetValue(Util.InfoHashToString(infohash), out tc))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Add torrent context for infohash.
        /// </summary>
        /// <param name="tc"></param>
        internal bool AddTorrentContext(TorrentContext tc)
        {
            return _torrents.TryAdd(Util.InfoHashToString(tc.infoHash), tc);
        }
        /// <summary>
        /// Remove torrent context for an infohash.
        /// </summary>
        /// <param name="tc"></param>
        /// <returns></returns>
        internal bool RemoveTorrentContext(TorrentContext tc)
        {
            return _torrents.TryRemove(Util.InfoHashToString(tc.infoHash), out TorrentContext _);
        }
        /// <summary>
        /// Get a remote peer given its infohash and ip.
        /// </summary>
        /// <param name="infoHash"></param>
        /// <param name="ip"></param>
        /// <param name="remotePeer"></param>
        /// <returns></returns>
        internal bool GetPeer(byte[] infoHash, string ip, out Peer remotePeer)
        {
            remotePeer = null;
            if (GetTorrentContext(infoHash, out TorrentContext tc))
            {
                if (tc.peerSwarm.TryGetValue(ip, out remotePeer))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Add peer to dead list.
        /// </summary>
        /// <param name="ip"></param>
        public void AddToDeadPeerList(string ip)
        {
            _deadPeers.Add(ip);
        }
        /// <summary>
        /// Remove peer from dead list. 
        /// </summary>
        /// <param name="ip"></param>
        public void RemoFromDeadPeerList(string ip)
        {
            _deadPeers.Remove(ip);
        }
        /// <summary>
        /// Is peer in dead list.
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public bool IsPeerDead(string ip)
        {
            return _deadPeers.Contains(ip);
        }
    }
}