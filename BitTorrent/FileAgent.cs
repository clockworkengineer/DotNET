using System;
using System.Text;
using System.Collections.Generic;

namespace BitTorrent
{
    public class FileAgent
    {
        public delegate void ProgessCallBack(Object progressData);

        private string _torrentFileName = String.Empty;
        private MetaInfoFile _torrentMetaInfo;
        private string _downloadPath;
        private Tracker _mainTracker;
        private AnnounceResponse _currentAnnouneResponse;
        private FileDownloader _fileToDownloader;
        private Peer _remotePeer;

        private List<FileDetails> _filesToDownload;

        private void assemblePiece(UInt32 pieceNumber)
        {
            Program.Logger.Debug($"Get blocks for piece {pieceNumber}.");

            UInt32 blockNumber = 0;
            for (; !_remotePeer.TorrentDownloader.Dc.isBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
            {    
                PWP.request(_remotePeer, pieceNumber, blockNumber * Constants.kBlockSize, Constants.kBlockSize);
            }

            PWP.request(_remotePeer, pieceNumber, blockNumber * Constants.kBlockSize, 
                         (UInt32)_fileToDownloader.Dc.pieceMap[pieceNumber].lastBlockLength);

            for (; !_fileToDownloader.Dc.hasPieceBeenAssembled(pieceNumber);) { }

            Program.Logger.Debug($"All blocks for piece {pieceNumber} received");

            _fileToDownloader.writePieceToFiles(pieceNumber);

        }

        private void connectToFirstWorkingPeer()
        {

            Program.Logger.Info("Connecting to first available peer....");

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
                    throw new Error("Failure trying to connect to peer.", ex);
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

            try
            {
                Program.Logger.Info("Loading MetaInfo for torrent file ....");

                _torrentMetaInfo = new MetaInfoFile(_torrentFileName);
                _torrentMetaInfo.load();
                _torrentMetaInfo.parse();

                Program.Logger.Info("Loading main tracker ....");

                _mainTracker = new Tracker(_torrentMetaInfo, PeerID.get());

                _filesToDownload = new List<FileDetails>();

                UInt32 pieceLength = UInt32.Parse(Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["piece length"]));

                Program.Logger.Info("Create files to download details structure ...");

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
                    UInt32 fileNo = 0;
                    UInt64 totalBytes = 0;
                    string name = Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict["name"]);
                    while (_torrentMetaInfo.MetaInfoDict.ContainsKey(fileNo.ToString()))
                    {
                        string[] details = Encoding.ASCII.GetString(_torrentMetaInfo.MetaInfoDict[fileNo.ToString()]).Split(',');
                        FileDetails fileDetail = new FileDetails();
                        fileDetail.name = _downloadPath + "/" + name + details[0];
                        fileDetail.length = UInt64.Parse(details[1]);
                        fileDetail.md5sum = details[2];
                        fileDetail.offset = totalBytes;
                        _filesToDownload.Add(fileDetail);
                        fileNo++;
                        totalBytes += fileDetail.length;
                    }

                }

                Program.Logger.Info("Setup file downloader ...");

                _fileToDownloader = new FileDownloader(_filesToDownload, pieceLength, _torrentMetaInfo.MetaInfoDict["pieces"]);

                _fileToDownloader.buildDownloadedPiecesMap();

                Program.Logger.Info("Initial main tracker announce ...");

                _currentAnnouneResponse = _mainTracker.announce();

                _mainTracker.startAnnouncing();

                connectToFirstWorkingPeer();

            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
                throw new Error("Failure in to load torrent File Agent.", ex);
            }


        }

        public void download(ProgessCallBack progressFunction = null, Object progressData = null)
        {

            try
            {
                Program.Logger.Info("Starting torrent download for MetaInfo data ...");

                _mainTracker.Event = Tracker.TrackerEvent.started;

                PWP.unchoke(_remotePeer);

                for (UInt32 nextPiece = 0; _fileToDownloader.selectNextPiece(ref nextPiece);)
                {
                    for (; _remotePeer.PeerChoking;) { }

                    assemblePiece((UInt32)nextPiece);

                    if (progressFunction != null)
                    {
                        progressFunction(progressData);
                    }

                    _mainTracker.Left = (UInt64)_fileToDownloader.Dc.totalLength - _fileToDownloader.Dc.totalBytesDownloaded;
                    _mainTracker.Downloaded = _fileToDownloader.Dc.totalBytesDownloaded;

                    Program.Logger.Info((_fileToDownloader.Dc.totalBytesDownloaded / (double)_fileToDownloader.Dc.totalLength).ToString("0.00%"));

                }

                _mainTracker.Event = Tracker.TrackerEvent.completed;

                Program.Logger.Info("Whole Torrent finished downloading.");
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
                throw new Error("Failure in File Agent torrent file download.", ex);
            }

        }

        public void close()
        {
            _mainTracker.stopAnnonncing();
            Program.Logger.Info("Closing peer socket.");
            _remotePeer.ReadFromRemotePeer = false;
            _remotePeer.PeerSocket.Close();
        }
    }
}
