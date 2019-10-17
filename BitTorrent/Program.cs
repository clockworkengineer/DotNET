using System;

namespace BitTorrent
{
    class MainClass
    {
        public static void Main(string[] args)
        {

            try
            {
                string peerID = PeerID.get();
                MetaInfoFile file1 = new MetaInfoFile("/home/robt/utorrent/Jobs Email Content.torrent");
                //MetaInfoFile file2 = new MetaInfoFile("./sample02.torrent");
                //MetaInfoFile file3 = new MetaInfoFile("./sample03.torrent");
                //MetaInfoFile file4 = new MetaInfoFile("./sample04.torrent");
                //MetaInfoFile file5 = new MetaInfoFile("./sample05.torrent");
                //MetaInfoFile file6 = new MetaInfoFile("./sample06.torrent");

                file1.load();
                file1.parse();
                //Console.WriteLine(new string('*', 80));
                //file2.load();
                //file2.parse();
                //Console.WriteLine(new string('*', 80));
                //file3.load();
                //file3.parse();
                //Console.WriteLine(new string('*', 80));
                //file4.load();
                //file4.parse();
                //Console.WriteLine(new string('*', 80));
                //file5.load();
                //file5.parse();
                //Console.WriteLine(new string('*', 80));
                //file6.load();
                //file6.parse();

                Tracker tracker1 = new Tracker(file1, "http://vimes:9000/announce", peerID);

                string status = tracker1.connect();

                Console.WriteLine(status);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
