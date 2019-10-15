using System;

namespace BitTorrent
{
    class MainClass
    {
        public static void Main(string[] args)
        {

            try
            {
                MetaInfoFile file1 = new MetaInfoFile("./sample01.torrent");
                MetaInfoFile file2 = new MetaInfoFile("./sample02.torrent");
                MetaInfoFile file3 = new MetaInfoFile("./sample03.torrent");
                MetaInfoFile file4 = new MetaInfoFile("./sample04.torrent");
                MetaInfoFile file5 = new MetaInfoFile("./sample05.torrent");
                MetaInfoFile file6 = new MetaInfoFile("./sample06.torrent");

                file1.load();
                file1.parse();
                Console.WriteLine(new string('*', 80));
                file2.load();
                file2.parse();
                Console.WriteLine(new string('*', 80));
                file3.load();
                file3.parse();
                Console.WriteLine(new string('*', 80));
                file4.load();
                file4.parse();
                Console.WriteLine(new string('*', 80));
                file5.load();
                file5.parse();
                Console.WriteLine(new string('*', 80));
                file6.load();
                file6.parse();

                Tracker tracker1 = new Tracker("http://torrent.resonatingmedia.com:6969/announce");
                Tracker tracker2 = new Tracker("http://torrent.resonatingmedia.com:6969/announce");
                Tracker tracker3 = new Tracker("http://torrent.resonatingmedia.com:6969/announce");
                Tracker tracker4 = new Tracker("http://torrent.resonatingmedia.com:6969/announce");
                Tracker tracker5 = new Tracker("http://torrent.resonatingmedia.com:6969/announce");
                Tracker tracker6 = new Tracker("http://torrent.resonatingmedia.com:6969/announce");

                tracker1.printInfoHash(file1);
                tracker2.printInfoHash(file2);
                tracker3.printInfoHash(file3);
                tracker4.printInfoHash(file4);
                tracker5.printInfoHash(file5);
                tracker6.printInfoHash(file6);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
