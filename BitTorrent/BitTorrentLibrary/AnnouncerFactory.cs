//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Factory for creating annoucers depending on the URL
// passed in. At present it is onef too types HTTP or UDP.
//
// Copyright 2020.
//
using System;
namespace BitTorrentLibrary
{
    internal class AnnouncerFactory
    {
        public AnnouncerFactory()
        {
        }
        public static IAnnouncer CreateAnnoucer(string url)
        {
            if (!url.StartsWith("http://"))
            {
                if (url.StartsWith("udp://"))
                {
                    Log.Logger.Info("(AnnouncerFactory) Main tracker is UDP...");
                    return new AnnouncerUDP(url, new UDP());
                }
                else
                {
                    throw new Exception("Error: Invalid tracker URL.");
                }
            }
            else
            {
                Log.Logger.Info("(Tracker) Main tracker is HTTP...");
                return new AnnouncerHTTP(url, new Web());
            }
        }
    }
}