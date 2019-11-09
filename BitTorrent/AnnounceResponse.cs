//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: 
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;

namespace BitTorrent
{
    public struct PeerDetails
    {
        public string _peerID;
        public string ip;
        public UInt32 port;
    }

    public struct AnnounceResponse
    {
        public UInt32 announceCount;
        public UInt32 statusCode;
        public string statusMessage;
        public UInt32 interval;
        public UInt32 minInterval;
        public string trackerID;
        public UInt32 complete;
        public UInt32 incomplete;
        public List<PeerDetails> peers;
    };
}
