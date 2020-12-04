//
// Author: Rob Tizzard
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
    internal struct PieceRequest
    {
        public byte[] infoHash;     // Torrent infohash
        public string ip;           // Swarm peer ip
        public UInt32 pieceNumber;  // Piece number
        public UInt32 blockOffset;  // Block Offset
        public UInt32 blockSize;    // Block Size
    }
}
