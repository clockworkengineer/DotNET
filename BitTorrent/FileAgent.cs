using System;
using System.Text;
using System.Collections.Generic;

namespace BitTorrent
{
    public class FileAgent
    {
        private string _torrentFileName = String.Empty;
        private MetaInfoFile _torrentMetaInfo;
        private string _downloadPath;
        private Tracker _mainTracker;
        private FileDownloader _fileToDownloader;
        private Peer _remotePeer;
        private AnnounceResponse _currentAnnouneResponse;
        private List<FileDetails> _filesToDownload;

        private void getBlocksOfPiece(int pieceNumber)
        {
            Program.Logger.Debug($"Get blocks for piece {pieceNumber}.");

            for (var blockNumber = 0; blockNumber < _fileToDownloader.Dc.pieceMap[pieceNumber].blocks.Length; blockNumber++)
            {
                PWP.request(_remotePeer, pieceNumber, blockNumber * Constants.kBlockSize, 
                            _fileToDownloader.Dc.pieceMap[pieceNumber].blocks[blockNumber].size);

                for(; ((_fileToDownloader.Dc.pieceMap[pieceNumber].blocks[blockNumber].flags & Mapping.NoneLocal)==Mapping.NoneLocal);) { }

                if (_fileToDownloader.Dc.totalBytesDownloaded >= _fileToDownloader.Dc.totalLength) { break; }

            }

            Program.Logger.Debug($"All blocks for piece {pieceNumber} received");

            _fileToDownloader.writePieceToFiles(pieceNumber);

        }

        private void connectToFirstWorkingPeer()
        {

            foreach (var peer in _currentAnnouneResponse.peers) {
                try
                {
                    _remotePeer = new Peer(_fileToDownloader, peer.ip, peer.port, _torrentMetaInfo.MetaInfoDict["info hash"]);
                    _remotePeer.connect();
                    if (_remotePeer.Connected) {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Program.Logger.Info($"Failed to connect to {peer.ip}");
                    Program.Logger.Debug(ex);
                }
            }

            if (_remotePeer.Connected)
            {
                Program.Logger.Info($"BTP: Local Peer [{ PeerID.get()}] to remote peer [{Encoding.ASCII.GetString(_remotePeer.RemotePeerID)}].");
            }

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

            connectToFirstWorkingPeer();


        }

        public void download()
        {

            PWP.unchoke(_remotePeer);

            while (_fileToDownloader.Dc.totalBytesDownloaded < _fileToDownloader.Dc.totalLength)
            {
                int nextPiece;
                for (;_remotePeer.PeerChoking;) { }
                nextPiece = _fileToDownloader.selectNextPiece();
                if (nextPiece==-1)
                {
                    break;
                }
                getBlocksOfPiece(nextPiece);
                Program.Logger.Info($"Downloaded {(int)((((double)_fileToDownloader.Dc.totalBytesDownloaded) / _fileToDownloader.Dc.totalLength) * 100.0)}%");
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
