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
using NLog;

namespace BitTorrent
{
    class Program
    {

        public static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static void annouceResponse(AnnounceResponse response)
        {
            Logger.Debug("\nAnnouce Response\n-------------");
            Logger.Debug("\nStatus: "+response.statusCode);
            Logger.Debug("Status Message: " + response.statusMessage);
            Logger.Debug("Interval: " + response.interval);
            Logger.Debug("Min Interval: " + response.minInterval);
            Logger.Debug("trackerID: " + response.trackerID);
            Logger.Debug("Complete: " + response.complete);
            Logger.Debug("Incomplete: " + response.incomplete);
            Logger.Debug("\nPeers\n------");
            foreach (var peer in response.peers)
            {
                if (peer._peerID != string.Empty)
                {
                    Logger.Debug("Peer ID: " + peer._peerID);
                }
                Logger.Debug("IP: " + peer.ip);
                Logger.Debug("Port: " + peer.port);

            }

        }
    

        public static void torrentHasInfo(MetaInfoFile metaFile)
        {
            byte[] infoHash = metaFile.MetaInfoDict["info hash"];

            StringBuilder hex = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
                hex.AppendFormat("{0:x2}", b);

            Logger.Debug("\nInfo Hash\n-----------\n");
            Logger.Debug(hex);
        }

        public static void torrentTrackers(MetaInfoFile metaFile)
        {
            byte[] tracker = metaFile.MetaInfoDict["announce"];
 
            Logger.Debug("\nTrackers\n--------\n");
            Logger.Debug(Encoding.ASCII.GetString(tracker));

            if (metaFile.MetaInfoDict.ContainsKey("announce-list"))
            {
                byte[] trackers = metaFile.MetaInfoDict["announce-list"];
                Logger.Debug(Encoding.ASCII.GetString(trackers));
            }
        }

        public static void Main(string[] args)
        {
        
            try
            {
                FileAgent fileAgent01 = new FileAgent("./mint.iso.torrent", "/home/robt/utorrent");
 
                fileAgent01.Load();

                fileAgent01.DownloadAsync();

                Console.ReadKey();

                fileAgent01.Close();
              
            }
            catch (Error ex)
            {
                Program.Logger.Error(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

        }
    }
}
