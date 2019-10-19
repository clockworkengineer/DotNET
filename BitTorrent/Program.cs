using System;
using System.Text;

namespace BitTorrent
{
    class MainClass
    {

        public static void annouceReply(Tracker.Response response)
        {
            Console.WriteLine("\nAnnouce Reply\n-------------");
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

            Console.WriteLine("Info Hash\n-----------\n");
            Console.WriteLine(hex);
        }

        public static void torrentTrackers(MetaInfoFile metaFile)
        {
            byte[] tracker = metaFile.MetaInfoDict["announce"];
            byte[] trackers = metaFile.MetaInfoDict["announce-list"];

            Console.WriteLine("\nTrackers\n--------\n");
            Console.WriteLine(Encoding.ASCII.GetString(tracker));
            Console.WriteLine(Encoding.ASCII.GetString(trackers));
        }

        public static void Main(string[] args)
        {

            try
            {
                string peerID = PeerID.get();
                MetaInfoFile file01 = new MetaInfoFile("./sample03.torrent");

                file01.load();
                file01.parse();

                //torrentHasInfo(file01);

                //torrentTrackers(file01);

                //Tracker tracker10 = new Tracker(file01, Encoding.ASCII.GetString(file01.MetaInfoDict["announce"]), peerID);

                //Tracker.Response status = tracker10.announce();

                //annouceReply(status);


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
