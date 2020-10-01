//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: 
//
// Copyright 2019.
//


using System;
using System.Text;
using System.Threading;
using System.IO;
using NLog;
using BitTorrentLibrary;

namespace BitTorrent
{
    static class Program
    {
        public static void AnnouceResponse(AnnounceResponse response)
        {
            BitTorrentLibrary.Log.Logger.Debug("\nAnnouce Response\n-------------");
            BitTorrentLibrary.Log.Logger.Debug("Status Message: " + response.statusMessage);
            BitTorrentLibrary.Log.Logger.Debug("Interval: " + response.interval);
            BitTorrentLibrary.Log.Logger.Debug("Min Interval: " + response.minInterval);
            BitTorrentLibrary.Log.Logger.Debug("trackerID: " + response.trackerID);
            BitTorrentLibrary.Log.Logger.Debug("Complete: " + response.complete);
            BitTorrentLibrary.Log.Logger.Debug("Incomplete: " + response.incomplete);
            BitTorrentLibrary.Log.Logger.Debug("\nPeers\n------");
            foreach (var peer in response.peers)
            {
                if (peer._peerID != string.Empty)
                {
                    BitTorrentLibrary.Log.Logger.Debug("Peer ID: " + peer._peerID);
                }
                BitTorrentLibrary.Log.Logger.Debug("IP: " + peer.ip);
                BitTorrentLibrary.Log.Logger.Debug("Port: " + peer.port);
            }
        }

        public static void TorrentHasInfo(MetaInfoFile metaFile)
        {
            byte[] infoHash = metaFile.MetaInfoDict["info hash"];

            StringBuilder hex = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
                hex.AppendFormat("{0:x2}", b);

            BitTorrentLibrary.Log.Logger.Debug("\nInfo Hash\n-----------\n");
            BitTorrentLibrary.Log.Logger.Debug(hex);
        }

        public static void TorrentTrackers(MetaInfoFile metaFile)
        {
            byte[] tracker = metaFile.MetaInfoDict["announce"];

            BitTorrentLibrary.Log.Logger.Debug("\nTrackers\n--------\n");
            BitTorrentLibrary.Log.Logger.Debug(Encoding.ASCII.GetString(tracker));

            if (metaFile.MetaInfoDict.ContainsKey("announce-list"))
            {
                byte[] trackers = metaFile.MetaInfoDict["announce-list"];
                BitTorrentLibrary.Log.Logger.Debug(Encoding.ASCII.GetString(trackers));
            }
        }

        public static void Main(string[] args)
        {

            try
            {
                for (var test = 0; test < 5 ; test++) {

                    if (File.Exists($"{Directory.GetCurrentDirectory()}/file.txt"))
                    {
                        File.Delete($"{Directory.GetCurrentDirectory()}/file.txt");
                    }


                    if (File.Exists("/home/robt/utorrent/arcolinux-v20.9.2.iso"))
                    {
                        File.Delete("/home/robt/utorrent/arcolinux-v20.9.2.iso");
                    }

                    Log.Logger.Info("Loading and parsing metainfo for torrent file ....");
                    MetaInfoFile torrentFile = new MetaInfoFile("/home/robt/torrent/mint.iso.torrent");

                    torrentFile.Load();
                    torrentFile.Parse();

                    Downloader downloader = new Downloader(torrentFile, "/home/robt/utorrent");
                    Agent agent = new Agent(torrentFile, downloader);

                    if (agent.BytesLeftToDownload() != 0)
                    {
                        Tracker tracker = new Tracker(agent);

                        tracker.StartAnnouncing();

                        agent.Download();

                        agent.Close();
                    }
                    else
                    {
                        Log.Logger.Info("Torrent has been fully downloaded.");
                    }
                }
            }
            catch (Error ex)
            {
                BitTorrentLibrary.Log.Logger.Error(ex.Message);
            }
            catch (Exception ex)
            {
                BitTorrentLibrary.Log.Logger.Error(ex);
            }
        }
    }
}
