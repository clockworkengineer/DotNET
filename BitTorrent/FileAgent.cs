//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The File Agent class contains all the high level processing
// like download and upload for torrent files.
//
// Copyright 2019.
//

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BitTorrent
{

    /// <summary>
    /// File agent class defintion.
    /// </summary>
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
        public MetaInfoFile TorrentMetaInfo { get => _torrentMetaInfo; set => _torrentMetaInfo = value; }
        public List<Peer> RemotePeers { get => _remotePeers; set => _remotePeers = value; }

        /// <summary>
        /// Assembles the pieces.
        /// </summary>
        /// <returns>The pieces.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="progressFunction">Progress function.</param>
        /// <param name="progressData">Progress data.</param>
        private async Task AssemblePieces(Peer remotePeer, ProgessCallBack progressFunction, Object progressData, CancellationTokenSource cancelPeerSource)
        {

            try
            {
                Program.Logger.Debug($"Running block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                CancellationToken cancelPeer = cancelPeerSource.Token;

                PWP.Unchoke(remotePeer);

                for (; !remotePeer.BitfieldReceived;) { };

                for (UInt32 nextPiece = 0; FileToDownloader.SelectNextPiece(remotePeer, ref nextPiece);)
                {

                    Program.Logger.Debug($"Assembling blocks for piece {nextPiece}.");

                    await Task.Run(() => { for (; remotePeer.PeerChoking;) { } });

                    UInt32 blockNumber = 0;
                    for (; !remotePeer.TorrentDownloader.Dc.IsBlockPieceLast(nextPiece, blockNumber); blockNumber++)
                    {
                        PWP.Request(remotePeer, nextPiece, blockNumber * Constants.kBlockSize, Constants.kBlockSize);
                    }

                    PWP.Request(remotePeer, nextPiece, blockNumber * Constants.kBlockSize,
                                 (UInt32)FileToDownloader.Dc.pieceMap[nextPiece].lastBlockLength);

                    await Task.Run(() => { for (; !FileToDownloader.Dc.HasPieceBeenAssembled(nextPiece);) {  } });

                    Program.Logger.Debug($"All blocks for piece {nextPiece} received");

                    FileToDownloader.WritePieceToFiles(remotePeer, nextPiece);

                    _mainTracker.Left = (UInt64)FileToDownloader.Dc.totalLength - FileToDownloader.Dc.totalBytesDownloaded;
                    _mainTracker.Downloaded = FileToDownloader.Dc.totalBytesDownloaded;

                    if (progressFunction != null)
                    {
                        progressFunction(progressData);
                    }

                    for (; !Downloading;) { if (cancelPeer.IsCancellationRequested) return; };

                    if (cancelPeer.IsCancellationRequested) return;

                    Program.Logger.Info((FileToDownloader.Dc.totalBytesDownloaded / (double)FileToDownloader.Dc.totalLength).ToString("0.00%"));

                }

                Program.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");
            }
            catch (Error ex)
            {
                Program.Logger.Error(ex.Message);
                cancelPeerSource.Cancel();
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex);
                cancelPeerSource.Cancel();
            }

        }

        /// <summary>
        /// Creates the and connect peers.
        /// </summary>
        private void CreateAndConnectPeers()
        {

            Program.Logger.Info("Connecting to available peers....");

            RemotePeers = new List<Peer>();
            foreach (var peer in CurrentAnnouneResponse.peers) {
                try
                {
                    Peer remotePeer = new Peer(FileToDownloader, peer.ip, peer.port, TorrentMetaInfo.MetaInfoDict["info hash"]);
                    remotePeer.Connect();
                    if (remotePeer.Connected) {
                        RemotePeers.Add(remotePeer);
                        Program.Logger.Info($"BTP: Local Peer [{ PeerID.get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                    }
                }
                catch (Exception)
                {
                    Program.Logger.Info($"Failed to connect to {peer.ip}");
                }
            }

            Program.Logger.Info($"Number of connected piers {RemotePeers.Count}");


        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:BitTorrent.FileAgent"/> class.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public FileAgent(string torrentFileName, String downloadPath)
        {
            _torrentFileName = torrentFileName;
            _downloadPath = downloadPath;
       
        }

        /// <summary>
        /// Load this instance.
        /// </summary>
        public void Load()
        {

            try
            {
                Program.Logger.Info("Loading MetaInfo for torrent file ....");

                TorrentMetaInfo = new MetaInfoFile(_torrentFileName);
                TorrentMetaInfo.Load();
                TorrentMetaInfo.Parse();

                Program.Logger.Info("Loading main tracker ....");

                _mainTracker = new Tracker(this);

                _filesToDownload = new List<FileDetails>();

                UInt32 pieceLength = UInt32.Parse(Encoding.ASCII.GetString(TorrentMetaInfo.MetaInfoDict["piece length"]));

                Program.Logger.Info("Create files to download details structure ...");

                if (!TorrentMetaInfo.MetaInfoDict.ContainsKey("0"))
                {
                    FileDetails fileDetail = new FileDetails();
                    fileDetail.name = _downloadPath + "/" + Encoding.ASCII.GetString(TorrentMetaInfo.MetaInfoDict["name"]);
                    fileDetail.length = UInt64.Parse(Encoding.ASCII.GetString(TorrentMetaInfo.MetaInfoDict["length"]));
                    fileDetail.offset = 0;
                    _filesToDownload.Add(fileDetail);
                }
                else
                {
                    UInt32 fileNo = 0;
                    UInt64 totalBytes = 0;
                    string name = Encoding.ASCII.GetString(TorrentMetaInfo.MetaInfoDict["name"]);
                    while (TorrentMetaInfo.MetaInfoDict.ContainsKey(fileNo.ToString()))
                    {
                        string[] details = Encoding.ASCII.GetString(TorrentMetaInfo.MetaInfoDict[fileNo.ToString()]).Split(',');
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

                FileToDownloader = new FileDownloader(_filesToDownload, pieceLength, TorrentMetaInfo.MetaInfoDict["pieces"]);

                FileToDownloader.BuildDownloadedPiecesMap();

                Program.Logger.Info("Initial main tracker announce ...");

                _mainTracker.Left = (UInt64)FileToDownloader.Dc.totalLength;

                CurrentAnnouneResponse = _mainTracker.Announce();

                _mainTracker.StartAnnouncing();

               // PeerDetails peerId = new PeerDetails();

               // peerId.ip = CurrentAnnouneResponse.peers[0].ip;
               // peerId.port = CurrentAnnouneResponse.peers[0].port;

               //CurrentAnnouneResponse.peers.Add(peerId);
               //CurrentAnnouneResponse.peers.Add(peerId);
               //CurrentAnnouneResponse.peers.Add(peerId);
                //CurrentAnnouneResponse.peers.Add(peerId);
                //CurrentAnnouneResponse.peers.Add(peerId);

                CreateAndConnectPeers();

                if (RemotePeers.Count==0)
                {
                    throw new Error("Error: No peers would connect.");
                }

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

        /// <summary>
        /// Download the specified progressFunction and progressData.
        /// </summary>
        /// <param name="progressFunction">Progress function.</param>
        /// <param name="progressData">Progress data.</param>
        public void Download(ProgessCallBack progressFunction = null, Object progressData = null)
        {

            try
            {
                List<Task> assembleTasks = new List<Task>();
                CancellationTokenSource cancelPeerSource = new CancellationTokenSource();

                Program.Logger.Info("Starting torrent download for MetaInfo data ...");

                _mainTracker.Event = Tracker.TrackerEvent.started;

                foreach (var peer in RemotePeers)
                {
                    assembleTasks.Add(AssemblePieces(peer, progressFunction, progressData, cancelPeerSource));
                }

                Task.WaitAll(assembleTasks.ToArray());

                if (!cancelPeerSource.IsCancellationRequested)
                {
                    _mainTracker.Event = Tracker.TrackerEvent.completed;
                    Program.Logger.Info("Whole Torrent finished downloading.");
                }
                else
                {
                    Program.Logger.Info("Download aborted.");
                    Close();
                }



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

        /// <summary>
        /// Loads the async.
        /// </summary>
        /// <returns>The async.</returns>
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

        /// <summary>
        /// Downloads the async.
        /// </summary>
        /// <param name="progressFunction">Progress function.</param>
        /// <param name="progressData">Progress data.</param>
        public async void DownloadAsync(ProgessCallBack progressFunction = null, Object progressData = null)
        {
            try
            {
                await Task.Run(() => Download(progressFunction, progressData));
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

        /// <summary>
        /// Close this instance.
        /// </summary>
        public void Close()
        {
            try
            {
                _mainTracker.StopAnnonncing();
                Program.Logger.Info("Closing peer socket.");
                foreach (var peer in RemotePeers)
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

        /// <summary>
        /// Start this instance.
        /// </summary>
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

        /// <summary>
        /// Stop this instance.
        /// </summary>
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
