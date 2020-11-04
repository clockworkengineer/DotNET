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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
namespace BitTorrentLibrary
{
    public class Manager
    {
        private readonly ConcurrentDictionary<string, TorrentContext> _torrents; // Torrents downloading/seeding

        public Manager()
        {
            _torrents = new ConcurrentDictionary<string, TorrentContext>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="infohash"></param>
        /// <returns></returns>
        internal TorrentContext Get(byte[] infohash)
        {
            if (_torrents.TryGetValue(Util.InfoHashToString(infohash), out TorrentContext tc))
            {
                return tc;
            }
            throw new Error("BitTorrent (Manager) Error: Could not get torrent context for " + Util.InfoHashToString(infohash));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        internal void Put(TorrentContext tc)
        {
            if (!_torrents.TryAdd(Util.InfoHashToString(tc.InfoHash), tc))
            {
                throw new Error("BitTorrent (Manager) Error: Could not put torrent context for " + Util.InfoHashToString(tc.InfoHash));
            }
        }
    }
}