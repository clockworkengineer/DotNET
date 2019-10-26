using System;
using System.Text;
using System.Threading;
namespace BitTorrent
{
    public class FileAgent
    {
        private string _torrentFileName;
        private MetaInfoFile _torrentMetaInfo;
        private string _downloadPath;
        private Tracker _mainTracker;
        private FileDownloader _fileToDownloader;
        private Peer _remotePeer;
        private Tracker.Response _currentAnnouneResponse;

        public string TorrentFileName { get => _torrentFileName; set => _torrentFileName = value; }
        public MetaInfoFile TorrentMetaInfo { get => _torrentMetaInfo; set => _torrentMetaInfo = value; }
        public string DownloadPath { get => _downloadPath; set => _downloadPath = value; }
        public Tracker MainTracker { get => _mainTracker; set => _mainTracker = value; }

        private void getBlocksOfPiece()
        {

        }

        public FileAgent(string torrentFileName, String downloadPath)
        {
            _torrentFileName = torrentFileName;
            _downloadPath = downloadPath;
        }

        public void load()
        {

            _torrentMetaInfo = new MetaInfoFile(_torrentFileName);
            _torrentMetaInfo.load();
            _torrentMetaInfo.parse();

            _mainTracker = new Tracker(_torrentMetaInfo, PeerID.get());

            string fileName = _downloadPath + "/" + Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["name"]);
            int fileLength = int.Parse(Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["length"]));
            int pieceLength = int.Parse(Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["piece length"]));
            _fileToDownloader = new FileDownloader(fileName, fileLength, pieceLength, _torrentMetaInfo.MetaInfoDict["pieces"]);

            _fileToDownloader.check();

            _currentAnnouneResponse =  _mainTracker.announce();

            _remotePeer = new Peer(_fileToDownloader, _currentAnnouneResponse.peers[0].ip, _currentAnnouneResponse.peers[0].port,
                                   _torrentMetaInfo.MetaInfoDict["info hash"]);

            _remotePeer.connect();


        }

        public void download()
        {

            PWP.unchoke(_remotePeer);

            while (_fileToDownloader.ReceivedMapEnries!=_fileToDownloader.ReceivedMap.Length)
            {
                while (_remotePeer.PeerChoking)
                {
                   // Console.WriteLine("Waiting");
                }
                int nextPiece = _fileToDownloader.selectNextPiece();
                Console.WriteLine($"Get Piece {nextPiece}");
                int pieceLength = int.Parse(Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["piece length"]));
                int numberOfBlocks = (pieceLength / 1024);
                if (pieceLength % 1024 != 0)
                {
                    numberOfBlocks++;
                }
                int blockOffset = 0;
           //     for (var block = 0; block < numberOfBlocks; block++)
          //      {
                    //while (_remotePeer.PeerChoking)
                    //{
                    //    // Console.WriteLine("Waiting");
                    //}
                    PWP.request(_remotePeer, nextPiece, blockOffset, 1024);
                    blockOffset += 1024;
                    //Thread.Sleep(5000);
             //   }
              //  Console.ReadKey();
            }
        }
    }
}
