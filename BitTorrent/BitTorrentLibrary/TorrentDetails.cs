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
        Started,
        Seeding,
        Downloading,
        Paused,
        Stopped
    }
    public struct TorrentDetails
    {
        public TorrentStatus status;
        public string fileName;
        public List<PeerDetails> peers;
        public UInt64 uploadedBytes;
        public UInt64 downloadedBytes;
        public UInt32 missingPiecesCount;
        public UInt32 swarmSize;
        public UInt32 deadPeers;
        public byte[] infoHash;
    }
}
