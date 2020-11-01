//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description:
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;

namespace BitTorrentLibrary
{
    public enum TorrentStatus
    {
        Initialised,    // Not te running
        Seeding,        // Waiting to recieve upload requests
        Downloading,    // Currently downloading torrent
        Paused,         // Torrent activitu paused
        Ended           // Torrend activity ended
    }
    public struct TorrentDetails
    {
        public TorrentStatus status;        // Current status
        public string fileName;             // Full file name
        public List<PeerDetails> peers;     // List of peers in its swarm
        public UInt64 uploadedBytes;        // Total bytes uploaed to peers
        public UInt64 downloadedBytes;      // Total bytes downloaded from peers
        public UInt32 missingPiecesCount;   // Number of missing pieces that need download
        public UInt32 swarmSize;            // Swarm size
        public UInt32 deadPeers;            // Number of dead peers 
        public byte[] infoHash;             // Torrent InfoHash
    }
}
