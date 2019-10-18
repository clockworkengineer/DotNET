using System;
using System.Text;

namespace BitTorrent
{
    class MainClass
    {
        public static void printInfoHash(MetaInfoFile metaFile)
        {
            byte[] infoHash = metaFile.MetaInfoDict["info hash"];

            StringBuilder hex = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
                hex.AppendFormat("{0:x2}", b);

            Console.WriteLine(hex);
        }

        public static void printTrackers(MetaInfoFile metaFile)
        {
            byte[] tracker = metaFile.MetaInfoDict["announce"];
            byte[] trackers = metaFile.MetaInfoDict["announce-list"];

            Console.WriteLine(Encoding.ASCII.GetString(tracker));
            Console.WriteLine(Encoding.ASCII.GetString(trackers));
        }

        public static void Main(string[] args)
        {

            try
            {
                string peerID = PeerID.get();
                MetaInfoFile file10 = new MetaInfoFile("./sample10.torrent");
                file10.load();
                file10.parse();

                printInfoHash(file10);
                printTrackers(file10);
                string[] trackers = Encoding.ASCII.GetString(file10.MetaInfoDict["announce-list"]).Split(',');
                Console.WriteLine(trackers[0]);
                Tracker tracker10 = new Tracker(file10, trackers[0], peerID);

                Tracker.Response status = tracker10.announce();

                //Console.WriteLine(status);
                //Bencoding.BNodeBase bNode = Bencoding.decode(Encoding.ASCII.GetBytes(status));
                //Console.WriteLine(Encoding.ASCII.GetString(Bencoding.encode(bNode)));

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
