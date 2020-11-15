//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description:   Tracker interface at the moment only HTTP/UDP actually
// suport this.
//
// Copyright 2020.
//
using System;
using System.Threading.Tasks;
namespace BitTorrentLibrary
{
    internal interface IAnnouncer
    {
        AnnounceResponse Announce(Tracker tracker);
    }
}
