//
// Author: Rob Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Factory for creating announcers depending on the URL
// passed in. At present it is one two types HTTP or UDP.
//
// Copyright 2020.
//
using System;
namespace BitTorrentLibrary
{
    internal interface IAnnouncerFactory
    {
        IAnnouncer Create(string url);
    }

    internal class AnnouncerFactory : IAnnouncerFactory
    {
        public AnnouncerFactory()
        {
        }
        public IAnnouncer Create(string url)
        {
            if (!url.StartsWith("http://"))
            {
                if (url.StartsWith("udp://"))
                {
                    Log.Logger.Info("Main tracker is UDP...");
                    return new AnnouncerUDP(url, new UDP());
                }
                else
                {
                    throw new Exception("Error: Invalid tracker URL.");
                }
            }
            else
            {
                Log.Logger.Info("Main tracker is HTTP...");
                return new AnnouncerHTTP(url, new Web());
            }
        }
    }
}