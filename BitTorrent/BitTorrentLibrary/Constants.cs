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
    public  static class Constants
    {
        static public readonly string PathSeparator = $"{Path.DirectorySeparatorChar}";    // Path separator for host
        public const int BlockSize = 1024*16;      // Client Block size
        public const int HashLength = 20;          // Length of SHA1 hash in bytes
        public const byte SizeOfUInt32 = 4;        // Number of bytes in wire protocol message length
        public const int ReadSocketTimeout = 5;    // Read socket timeout in seconds

    }
}
