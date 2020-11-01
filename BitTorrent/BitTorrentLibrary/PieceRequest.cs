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
        public Peer remotePeer;     // Remote peer asking request
        public UInt32 pieceNumber;  // Piece number
        public UInt32 blockOffset;  // Block Offset
        public UInt32 blockSize;    // Block Size
        
    }
}
