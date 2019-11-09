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

namespace BitTorrent
{
    public struct FileDetails
    {
        public string name;
        public UInt64 length;
        public string md5sum;
        public UInt64 offset;
    }
}
