using System;
using System.Text;

namespace BitTorrent
{
    class MainClass
    {

        public static void annouceResponse(Tracker.Response response)
        {
            Console.WriteLine("\nAnnouce Response\n-------------");
            Console.WriteLine("\nStatus: "+response.statusCode);
            Console.WriteLine("Status Message: " + response.statusMessage);
            Console.WriteLine("Interval: " + response.interval);
            Console.WriteLine("Min Interval: " + response.minInterval);
            Console.WriteLine("trackerID: " + response.trackerID);
            Console.WriteLine("Complete: " + response.complete);
            Console.WriteLine("Incomplete: " + response.incomplete);
            Console.WriteLine("\nPeers\n------");
            foreach (var peer in response.peers)
            {
                if (peer._peerID != string.Empty)
                {
                    Console.WriteLine("Peer ID: " + peer._peerID);
                }
                Console.WriteLine("IP: " + peer.ip);
                Console.WriteLine("Port: " + peer.port);

            }

        }
    

        public static void torrentHasInfo(MetaInfoFile metaFile)
        {
            byte[] infoHash = metaFile.MetaInfoDict["info hash"];

            StringBuilder hex = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
                hex.AppendFormat("{0:x2}", b);

            Console.WriteLine("\nInfo Hash\n-----------\n");
            Console.WriteLine(hex);
        }

        public static void torrentTrackers(MetaInfoFile metaFile)
        {
            byte[] tracker = metaFile.MetaInfoDict["announce"];
 
            Console.WriteLine("\nTrackers\n--------\n");
            Console.WriteLine(Encoding.ASCII.GetString(tracker));

            if (metaFile.MetaInfoDict.ContainsKey("announce-list"))
            {
                byte[] trackers = metaFile.MetaInfoDict["announce-list"];
                Console.WriteLine(Encoding.ASCII.GetString(trackers));
            }
        }

        public static void Main(string[] args)
        {

            try
            {
                MetaInfoFile file01 = new MetaInfoFile("./wired.torrent");

                file01.load();
                file01.parse();

                torrentHasInfo(file01);

                torrentTrackers(file01);

                Tracker tracker10 = new Tracker(file01, PeerID.get());

                Tracker.Response status = tracker10.announce();

                //if (status.peers.Count > 0)
                //{
                //    Peer peer01 = new Peer(status.peers[0].ip, status.peers[0].port, file01.MetaInfoDict["info hash"]);
                //    peer01.connect();
                //}

                //annouceResponse(status);
           

                //tracker10.Interval = status.interval;

                //tracker10.startAnnouncing();

                //Console.ReadKey();

             
                //tracker10.stopAnnonncing();

              


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
