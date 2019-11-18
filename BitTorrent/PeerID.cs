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
    public static class PeerID
    {

        static public string get()
        {
            return ("-AZ1000-" + Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 12));
        }
    }
}
