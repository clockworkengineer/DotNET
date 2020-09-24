//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Details associated with each file in a torrent to download
//
// Copyright 2019.
//

using System;

namespace BitTorrentLibrary
{
    public struct FileDetails
    {
        public string name;     // File file name path
        public UInt64 length;   // File length in bytes
        public string md5sum;   // Checksum for file (optional)
        public UInt64 offset;   // Offset within torrent stream of file start
    }
}
