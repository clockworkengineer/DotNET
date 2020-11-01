//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: BitTorrent related constants.
//
// Copyright 2020.
//

using System;
using System.IO;

namespace BitTorrentLibrary
{
    internal static class Constants
    {
        public const int BlockSize = 1024 * 16;      // Client Block size
        public const int HashLength = 20;            // Length of SHA1 hash in bytes
        public const int PeerIDLength = 20;          // Length of peer ID hash in bytes
        public const byte SizeOfUInt32 = 4;          // Number of bytes in wire protocol message length
        public const int ReadSocketTimeout = 5;      // Read socket timeout in 
        public const int MaximumSwarmSize = 50;      // Maximum peer swarm size
        public const int IntialHandshakeLength = 68; // Length of intial peer to peer handshake

    }
}
