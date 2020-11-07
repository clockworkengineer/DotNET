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
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace BitTorrentLibrary
{
    public class Manager
    {
        private readonly ConcurrentDictionary<string, TorrentContext> _torrents; // Torrents downloading
        private readonly HashSet<string> _deadPeers;                             // Dead peers list
        internal Int32 DeadPeerCount => _deadPeers.Count;                        // Number of dead 
        internal ICollection<TorrentContext> TorrentList => _torrents.Values;    // List of torrent contexts

        public Manager()
        {
            _torrents = new ConcurrentDictionary<string, TorrentContext>();
            _deadPeers = new HashSet<string>
            {
                "192.168.1.1" // WITHOUT THIS HANGS (FOR ME)
            };
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
            return _torrents.TryAdd(Util.InfoHashToString(tc.InfoHash), tc);

        }
        /// <summary>
        /// Remove torrent context for an infohash.
        /// </summary>
        /// <param name="tc"></param>
        /// <returns></returns>
        internal bool RemoveTorrentContext(TorrentContext tc)
        {
            return _torrents.TryRemove(Util.InfoHashToString(tc.InfoHash), out TorrentContext _);
        }
        /// <summary>
        /// Add peer to dead list.
        /// </summary>
        /// <param name="ip"></param>
        internal void AddToDeadPeerList(string ip)
        {
            _deadPeers.Add(ip);
        }
        /// <summary>
        /// Remove peer from dead list. 
        /// </summary>
        /// <param name="ip"></param>
        internal void RemoFromDeadPeerList(string ip)
        {
            _deadPeers.Remove(ip);
        }
        /// <summary>
        /// Is peer in dead list.
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        internal bool IsPeerDead(string ip)
        {
            return _deadPeers.Contains(ip);
        }

    }
}