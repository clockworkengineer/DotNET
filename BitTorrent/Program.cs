using System;

namespace BitTorrent
{
    class MainClass
    {
        public static void Main(string[] args)
        {

            try
            {
                //MetaInfoFile file1 = new MetaInfoFile("./manjaro-cinnamon-18.0.3-stable-x86_64.iso.torrent");
                MetaInfoFile file1 = new MetaInfoFile("./Jobs Email Content.torrent");

                MetaInfoFile file2 = new MetaInfoFile("./ContactsDB.torrent");
                MetaInfoFile file3 = new MetaInfoFile("./sample.torrent");
                MetaInfoFile file4 = new MetaInfoFile("./sample2.torrent");
                MetaInfoFile file5 = new MetaInfoFile("./sample3.torrent");

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

                Tracker tracker1 = new Tracker("http://torrent.resonatingmedia.com:6969/announce");

                tracker1.createInfoHash(file1);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
