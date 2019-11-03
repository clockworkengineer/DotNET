using System;
using System.Text;
using System.Collections.Generic;

namespace BitTorrent
{
    public class FileAgent
    {

        public class FileDetails
        {
            public string name;
            public UInt64 length;
            public string md5sum;
            public UInt64 offset;
        }

        private string _torrentFileName;
        private MetaInfoFile _torrentMetaInfo;
        private string _downloadPath;
        private Tracker _mainTracker;
        private FileDownloader _fileToDownloader;
        private Peer _remotePeer;
        private Tracker.Response _currentAnnouneResponse;
        private List<FileDetails> _filesToDownload;

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
            Program.Logger.Debug($"Get blocks for piece {pieceNumber}.");

            for (var blockNumber = 0; blockNumber < _fileToDownloader.ReceivedMap[pieceNumber].blocks.Length; blockNumber++)
            {
                PWP.request(_remotePeer, pieceNumber, blockNumber * Constants.kBlockSize, _fileToDownloader.ReceivedMap[pieceNumber].blocks[blockNumber].size);
                for(; !_fileToDownloader.ReceivedMap[pieceNumber].blocks[blockNumber].mapped;) { }
                if (_fileToDownloader.TotalBytesDownloaded >= _fileToDownloader.Length) { break; }

            }

            Program.Logger.Debug($"All blocks for piece {pieceNumber} received");

            _fileToDownloader.writePieceToFiles(pieceNumber);

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

            _filesToDownload = new List<FileDetails>();

            int pieceLength = int.Parse(Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["piece length"]));

            if (!_torrentMetaInfo.MetaInfoDict.ContainsKey("0"))
            {
                FileDetails fileDetail = new FileDetails();
                fileDetail.name = _downloadPath + "/" + Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["name"]);
                fileDetail.length = UInt64.Parse(Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["length"]));
                fileDetail.offset = 0;
                _filesToDownload.Add(fileDetail);
            }
            else
            {
                int fileNo = 0;
                UInt64 totalBytes = 0;
                string name = Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["name"]);
                while (_torrentMetaInfo.MetaInfoDict.ContainsKey(fileNo.ToString()))
                {
                    string[] details = Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict[fileNo.ToString()]).Split(',');
                    FileDetails fileDetail = new FileDetails();
                    fileDetail.name = _downloadPath + "/" + name+ details[0];
                    fileDetail.length = UInt64.Parse(details[1]);
                    fileDetail.md5sum = details[2];
                    fileDetail.offset = totalBytes;
                    _filesToDownload.Add(fileDetail);
                    fileNo++;
                    totalBytes += fileDetail.length;
                }

            }

            _fileToDownloader = new FileDownloader(_filesToDownload, pieceLength, _torrentMetaInfo.MetaInfoDict["pieces"]);

            _fileToDownloader.buildDownloadedPiecesMap();

            _currentAnnouneResponse =  _mainTracker.announce();

            try
            {
                _remotePeer = new Peer(_fileToDownloader, _currentAnnouneResponse.peers[0].ip, _currentAnnouneResponse.peers[0].port,
                                       _torrentMetaInfo.MetaInfoDict["info hash"]);
            }
            catch (Exception ex)
            {
                _remotePeer = new Peer(_fileToDownloader, _currentAnnouneResponse.peers[1].ip, _currentAnnouneResponse.peers[1].port,
                                       _torrentMetaInfo.MetaInfoDict["info hash"]);
            }

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
                Program.Logger.Debug($"{(int)((((double)_fileToDownloader.TotalBytesDownloaded) / _fileToDownloader.Length) * 100.0)}%");
            }
            Program.Logger.Debug("File downloaded.");
        
        }

        public void close()
        {
            _remotePeer.ReadFromRemotePeer = false;
            _remotePeer.PeerSocket.Close();
        }
    }
}
