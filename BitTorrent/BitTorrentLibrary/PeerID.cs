//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Generate peer ID for a client.
//
// Copyright 2020.
//

using System;

namespace BitTorrentLibrary
{
    public static class PeerID
    {
        private static string _peerID = "-AZ1000-BMt9tgTUwEiH";
        static internal string Get()
        {
            return _peerID;
        }

        static public void SetPeerID(string peerID)
        {
            _peerID = peerID.Substring(0, 20);
        }
    }
}
