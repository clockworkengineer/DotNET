using System;

namespace BitTorrent
{
    class MainClass
    {
        public static void Main(string[] args)
        {

            try
            {
                //MetaInfoFile file = new MetaInfoFile("./manjaro-cinnamon-18.0.3-stable-x86_64.iso.torrent");
                MetaInfoFile file = new MetaInfoFile("./ContactsDB.torrent");
                //MetaInfoFile file = new MetaInfoFile("./sample.torrent");

                file.load();
                file.parse();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
