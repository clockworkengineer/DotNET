//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description:  Peer ID for a client. This should be a static and not generated
// at runtime but there is a means of modifying it at runtime through SetPeerID.
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
