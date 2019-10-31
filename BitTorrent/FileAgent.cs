using System;
using System.Text;
using System.IO;
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

        private int blockSizeToDownload(int pieceNumber)
        {
            int blockSize = Constants.kBlockSize;

            if (pieceNumber == _fileToDownloader.NumberOfPieces - 1)
            {
                if (_fileToDownloader.Length - _fileToDownloader.TotalBytesDownloaded < (UInt64)Constants.kBlockSize)
                {
                    blockSize = (int) (_fileToDownloader.Length - _fileToDownloader.TotalBytesDownloaded);
                }
            }

            return (blockSize);
      
        }     

        private void getBlocksOfPiece(int pieceNumber)
        {
            Console.WriteLine($"Get blocks for piece {pieceNumber}.");

            for (var blockNumber = 0; blockNumber < _fileToDownloader.BlocksPerPiece; blockNumber++)
            {
                PWP.request(_remotePeer, pieceNumber, blockNumber * Constants.kBlockSize, blockSizeToDownload(pieceNumber));
                for(; !_fileToDownloader.ReceivedMap[pieceNumber].blocks[blockNumber];) { }
                if (_fileToDownloader.TotalBytesDownloaded >= _fileToDownloader.Length) { break; }

            }

            Console.WriteLine($"All blocks for piece {pieceNumber} received");

            _fileToDownloader.writePieceToFile(pieceNumber);

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
            UInt64 fileLength = UInt64.Parse(Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["length"]));
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

            while (_fileToDownloader.TotalBytesDownloaded < _fileToDownloader.Length)
            {
                int nextPiece;
                for (;_remotePeer.PeerChoking;) { }
                nextPiece = _fileToDownloader.selectNextPiece();
                if (nextPiece==-1)
                {
                    break;
                }
                getBlocksOfPiece(nextPiece);
                Console.WriteLine((int)((((double)_fileToDownloader.TotalBytesDownloaded)/_fileToDownloader.Length)*100.0));
            }
            Console.WriteLine("File downloaded.");
        
        }

        public void close()
        {
            _remotePeer.ReadFromRemotePeer = false;
            _remotePeer.PeerSocket.Close();
        }
    }
}
