//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Structure containing details returned from a announce request 
// to a tracker server.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;

namespace BitTorrent
{
    /// <summary>
    /// Peer details.
    /// </summary>
    public struct PeerDetails
    {
        public string _peerID;  // ID (OPTIONAL)
        public string ip;       // IP Address
        public UInt32 port;     // Port
    }

    /// <summary>
    /// Announce response.
    /// </summary>
    public struct AnnounceResponse
    {
        public UInt32 announceCount;    // Announce counter
        public UInt32 statusCode;       // Returned status get
        public string statusMessage;    // Returned status/error message
        public UInt32 interval;         // Poll time between annouces in milliseconds
        public UInt32 minInterval;      // Minimum poll time
        public string trackerID;        // Track ID (OPTIONAL)
        public UInt32 complete;         // Number of seeders for torrent (OPTIONAL)
        public UInt32 incomplete;       // Number of non-seeder peers (leeches) (OPTIONAL)
        public List<PeerDetails> peers; // Number of peers in swarm
    };
}
