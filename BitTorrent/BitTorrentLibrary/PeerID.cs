//
// Author: Rob Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description:  Peer ID for a client.
//
// Copyright 2020.
//
using System;
namespace BitTorrentLibrary
{
    public static class PeerID
    {
        static internal string Get()
        {
            return "-AZ1000-" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 12);
        }
    }
}
