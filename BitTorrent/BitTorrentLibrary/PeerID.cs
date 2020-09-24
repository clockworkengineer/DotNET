//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Generate peer ID for a client.
//
// Copyright 2019.
//

using System;

namespace BitTorrentLibrary
{
    public static class PeerID
    {
        static public string Get()
        {
            return "-AZ1000-" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 12);
        }
    }
}
