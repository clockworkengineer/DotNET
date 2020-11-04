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
        internal bool Get(byte[] infohash, out TorrentContext tc)
        {
            if (_torrents.TryGetValue(Util.InfoHashToString(infohash),  out tc))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        internal bool Add(TorrentContext tc)
        {
            return _torrents.TryAdd(Util.InfoHashToString(tc.InfoHash), tc);
  
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <returns></returns>
        internal bool Remove(TorrentContext tc) {
            return _torrents.TryRemove(Util.InfoHashToString(tc.InfoHash), out TorrentContext _);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal ICollection<TorrentContext> GetTorrentList()
        {
            return _torrents.Values;
        }
    }
}