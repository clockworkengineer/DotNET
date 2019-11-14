//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: 
//
// Copyright 2019.
//

using System;
using System.Text;
using System.Threading.Tasks;
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
        private List<Peer> _remotePeers;
        private List<FileDetails> _filesToDownload;
        private bool _downloading = true;

        public FileDownloader FileToDownloader { get => _fileToDownloader; set => _fileToDownloader = value; }
        public bool Downloading { get => _downloading;  }
        public AnnounceResponse CurrentAnnouneResponse { get => _currentAnnouneResponse; set => _currentAnnouneResponse = value; }

        private void AssemblePiece(Peer remotePeer, UInt32 pieceNumber)
        {
            Program.Logger.Debug($"Get blocks for piece {pieceNumber}.");

            for (; remotePeer.PeerChoking;) { }

            UInt32 blockNumber = 0;
            for (; !remotePeer.TorrentDownloader.Dc.IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
            {    
                PWP.request(remotePeer, pieceNumber, blockNumber * Constants.kBlockSize, Constants.kBlockSize);
            }

            PWP.request(remotePeer, pieceNumber, blockNumber * Constants.kBlockSize, 
                         (UInt32)FileToDownloader.Dc.pieceMap[pieceNumber].lastBlockLength);

            for (; !FileToDownloader.Dc.HasPieceBeenAssembled(pieceNumber);) { }

            Program.Logger.Debug($"All blocks for piece {pieceNumber} received");

            FileToDownloader.WritePieceToFiles(pieceNumber);

            _mainTracker.Left = (UInt64)FileToDownloader.Dc.totalLength - FileToDownloader.Dc.totalBytesDownloaded;
            _mainTracker.Downloaded = FileToDownloader.Dc.totalBytesDownloaded;

            Program.Logger.Info((FileToDownloader.Dc.totalBytesDownloaded / (double)FileToDownloader.Dc.totalLength).ToString("0.00%"));


        }

        private void CreateAndConnectPeers()
        {

            Program.Logger.Info("Connecting to first available peer....");

            _remotePeers = new List<Peer>();
            foreach (var peer in CurrentAnnouneResponse.peers) {
                try
                {
                    Peer remotePeer = new Peer(FileToDownloader, peer.ip, peer.port, _torrentMetaInfo.MetaInfoDict["info hash"]);
                    remotePeer.Connect();
                    if (remotePeer.Connected) {
                        _remotePeers.Add(remotePeer);
                        Program.Logger.Info($"BTP: Local Peer [{ PeerID.get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");

                    }
                }
                catch (Exception)
                {
                    Program.Logger.Info($"Failed to connect to {peer.ip}");
                }
            }
         

        }

        public FileAgent(string torrentFileName, String downloadPath)
        {
            _torrentFileName = torrentFileName;
            _downloadPath = downloadPath;
       
        }

        public void Load()
        {

            try
            {
                Program.Logger.Info("Loading MetaInfo for torrent file ....");

                _torrentMetaInfo = new MetaInfoFile(_torrentFileName);
                _torrentMetaInfo.Load();
                _torrentMetaInfo.Parse();

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

                FileToDownloader = new FileDownloader(_filesToDownload, pieceLength, _torrentMetaInfo.MetaInfoDict["pieces"]);

                FileToDownloader.BuildDownloadedPiecesMap();

                Program.Logger.Info("Initial main tracker announce ...");

                CurrentAnnouneResponse = _mainTracker.announce();

                _mainTracker.startAnnouncing();

                CreateAndConnectPeers();

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
                throw new Error("Failure in to load torrent File Agent.", ex);
            }


        }

        async public Task DownloadAsync(ProgessCallBack progressFunction = null, Object progressData = null)
        {

            try
            {
                Program.Logger.Info("Starting torrent download for MetaInfo data ...");

                _mainTracker.Event = Tracker.TrackerEvent.started;

                foreach (var peer in _remotePeers)
                {
                    PWP.unchoke(peer);
                }

                for (UInt32 nextPiece = 0; FileToDownloader.SelectNextPiece(ref nextPiece);)
                {
                    await Task.Run(() => AssemblePiece(_remotePeers[0], (UInt32)nextPiece));
                    if (progressFunction != null) progressFunction(progressData);
                    for (; !Downloading;) { }
                }

                _mainTracker.Event = Tracker.TrackerEvent.completed;

                Program.Logger.Info("Whole Torrent finished downloading.");

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
                throw new Error("Failure in File Agent torrent file download.", ex);
            }


        }

        public async Task LoadAsync()
        {
            try
            {
                await Task.Run(() => Load());
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void Download(ProgessCallBack progressFunction = null, Object progressData = null)
        {
            try
            {
                Download(progressFunction, progressData);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void Close()
        {
            try
            {
                _mainTracker.stopAnnonncing();
                Program.Logger.Info("Closing peer socket.");
                foreach (var peer in _remotePeers)
                {
                    peer.ReadFromRemotePeer = false;
                    peer.PeerSocket.Close();
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void Start()
        {
            try
            {
                _downloading = true;
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void Stop()
        {
            try
            {
                _downloading = false;
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }
    }
}
