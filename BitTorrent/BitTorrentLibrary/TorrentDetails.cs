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
    public struct TorrentDetails
    {
        public List<PeerDetails> peers;
        public UInt64 uploadedBytes;
        public UInt64 downloadedBytes;
        public UInt32 missingPiecesCount;
        public byte[] infoHash;
    }
}
