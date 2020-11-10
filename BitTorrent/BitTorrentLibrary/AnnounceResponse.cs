//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Structure containing details returned from a announce request 
// to a tracker server.If failure is returned true then the annouce request
// has been rejected by the tracker and the reason will be put into statusMessage.
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;

namespace BitTorrentLibrary
{
    /// <summary>
    /// Peer information.
    /// </summary>
    public struct PeerDetails
    {
        public byte[] infoHash; // Torrent infohash
        public string peerID;   // ID (optional)
        public string ip;       // IP Address
        public UInt32 port;     // Port
    }

    /// <summary>
    /// Announce response.
    /// </summary>
    internal struct AnnounceResponse
    {
        public UInt32 announceCount;    // Announce counter
        public bool failure;            // == true tracker failure message returned
        public string statusMessage;    // Returned failure/warning message message
        public UInt32 interval;         // Poll time between annouces in milliseconds
        public UInt32 minInterval;      // Minimum poll time
        public string trackerID;        // Track ID (optional)
        public UInt32 complete;         // Number of seeders for torrent (optional)
        public UInt32 incomplete;       // Number of non-seeder peers (leeches) (optional)
        public List<PeerDetails> peers; // Number of peers in swarm
    };
}
