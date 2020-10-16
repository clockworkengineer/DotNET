//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Piece request recieved from remote peer to be queued and processed.
//
// Copyright 2020.
//
using System;

namespace BitTorrentLibrary
{
    public struct PieceRequest
    {
        public Peer remotePeer;
        public UInt32 pieceNumber;
        public UInt32 blockOffset;
        public UInt32 blockSize;
        
    }
}
