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
    /// File agent class definition.
    /// </summary>
    public class FileAgent
    {
        public delegate void ProgessCallBack(Object progressData);

        private MetaInfoFile _torrentMetaInfo;              // Torrent Metafile information
        private string _downloadPath;                       // Download root patch
        private Tracker _mainTracker;                       // Main torrent tracker
        private AnnounceResponse _currentAnnouneResponse;   // Current tracker annouce respose
        private FileDownloader _fileToDownloader;           // FileDownloader for torrent
        private List<Peer> _remotePeers;                    // Connected remote peers
        private List<FileDetails> _filesToDownload;         // Files to download in torrent
        private ManualResetEvent _downloading;              // WaitOn when downloads == false

        public FileDownloader FileToDownloader { get => _fileToDownloader; set => _fileToDownloader = value; }
        public AnnounceResponse CurrentAnnouneResponse { get => _currentAnnouneResponse; set => _currentAnnouneResponse = value; }
        public MetaInfoFile TorrentMetaInfo { get => _torrentMetaInfo; set => _torrentMetaInfo = value; }
        public List<Peer> RemotePeers { get => _remotePeers; set => _remotePeers = value; }
        public ManualResetEvent Downloading { get => _downloading; set => _downloading = value; }

        /// <summary>
        /// Assembles the pieces of a torrent block by block.A task is created using this method for each connected peer.
        /// </summary>
        /// <returns>Task reference on completion.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="progressFunction">Progress function.</param>
        /// <param name="progressData">Progress data.</param>
        private void AssemblePieces(Peer remotePeer, ProgessCallBack progressFunction, Object progressData, CancellationTokenSource cancelAssemblerTaskSource)
        {

            try
            {
                Program.Logger.Debug($"Running block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                CancellationToken cancelAssemblerTask = cancelAssemblerTaskSource.Token;

                PWP.Unchoke(remotePeer);

                remotePeer.BitfieldReceived.WaitOne();

                remotePeer.PeerChoking.WaitOne();

                while (_mainTracker.Left != 0)
                {
                    for (UInt32 nextPiece = 0; FileToDownloader.SelectNextPiece(remotePeer, ref nextPiece);)
                    {

                        Program.Logger.Debug($"Assembling blocks for piece {nextPiece}.");

                        remotePeer.Active = true;
                        remotePeer.TransferingPiece = nextPiece;

                        UInt32 blockNumber = 0;
                        for (; !remotePeer.TorrentDownloader.Dc.IsBlockPieceLast(nextPiece, blockNumber); blockNumber++)
                        {
                            PWP.Request(remotePeer, nextPiece, blockNumber * Constants.kBlockSize, Constants.kBlockSize);
                        }

                        PWP.Request(remotePeer, nextPiece, blockNumber * Constants.kBlockSize,
                                     FileToDownloader.Dc.pieceMap[nextPiece].lastBlockLength);

                        remotePeer.WaitForPieceAssembly.WaitOne();
                        remotePeer.WaitForPieceAssembly.Reset();

                        remotePeer.TransferingPiece = -1;

                        Program.Logger.Debug($"All blocks for piece {nextPiece} received");

                        PieceBuffer pieceBuffer = new PieceBuffer(remotePeer.AssembledPiece);
                        _fileToDownloader.Dc.pieceBufferWriteQueue.Add(pieceBuffer);

                        _mainTracker.Left = FileToDownloader.Dc.totalLength - FileToDownloader.Dc.totalBytesDownloaded;
                        _mainTracker.Downloaded = FileToDownloader.Dc.totalBytesDownloaded;

                        if (progressFunction != null)
                        {
                            progressFunction(progressData);
                        }

                        if (cancelAssemblerTask.IsCancellationRequested)
                        {
                            foreach (var peer in RemotePeers)
                            {
                                if (!peer.PeerChoking.WaitOne(0))
                                {
                                    peer.PeerChoking.Set();
                                }
                            }
                            return;
                        }

                        Program.Logger.Info((FileToDownloader.Dc.totalBytesDownloaded / (double)FileToDownloader.Dc.totalLength).ToString("0.00%"));

                        Downloading.WaitOne();

                        remotePeer.PeerChoking.WaitOne();

                    }
              
                    foreach (var peer in RemotePeers)
                    {  
                        if (!peer.PeerChoking.WaitOne(0))
                        {
                            peer.PeerChoking.Set();
                        }
                    }
                    
                }

                Program.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

            }
            catch (Error ex)
            {
                Program.Logger.Error(ex.Message);
                cancelAssemblerTaskSource.Cancel();
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex);
                cancelAssemblerTaskSource.Cancel();
            }

       

        }

        /// <summary>
        /// Creates the and connect remote torrent peers.
        /// </summary>
        private void CreateAndConnectPeers()
        {

            Program.Logger.Info("Connecting to available peers....");

            RemotePeers = new List<Peer>();
            foreach (var peer in CurrentAnnouneResponse.peers)
            {
                try
                {
                    Peer remotePeer = new Peer(FileToDownloader, peer.ip, peer.port, TorrentMetaInfo.MetaInfoDict["info hash"]);
                    remotePeer.Connect();
                    if (remotePeer.Connected)
                    {
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
        /// Initializes a new instance of the FileAgent class.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public FileAgent(string torrentFileName, String downloadPath)
        {
            TorrentMetaInfo = new MetaInfoFile(torrentFileName);
            _downloadPath = downloadPath;
            Downloading = new ManualResetEvent(true);
        }

        /// <summary>
        /// Load FileAgent instance.
        /// </summary>
        public void Load()
        {

            try
            {
                Program.Logger.Info("Loading and parsing metainfo for torrent file ....");
                
                TorrentMetaInfo.Parse();

                Program.Logger.Info("Loading main tracker ....");

                _mainTracker = new Tracker(this);

                _filesToDownload = new List<FileDetails>();

                UInt32 pieceLength = UInt32.Parse(Encoding.ASCII.GetString(TorrentMetaInfo.MetaInfoDict["piece length"]));

                Program.Logger.Info("Create files to download details structure ...");

                if (!TorrentMetaInfo.MetaInfoDict.ContainsKey("0"))
                {
                    FileDetails fileDetail = new FileDetails();
                    fileDetail.name = _downloadPath + Constants.kPathSeparator + Encoding.ASCII.GetString(TorrentMetaInfo.MetaInfoDict["name"]);
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
                        fileDetail.name = _downloadPath + Constants.kPathSeparator + name + details[0];
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

                _mainTracker.Left = FileToDownloader.Dc.totalLength-FileToDownloader.Dc.totalBytesDownloaded;
                if (_mainTracker.Left==0)
                {
                    Program.Logger.Info("Torrent file fully downloaded already.");
                    return;
                }

                CurrentAnnouneResponse = _mainTracker.Announce();

                _mainTracker.StartAnnouncing();

                ////Simulate multipeer downloads
                //PeerDetails peerId = new PeerDetails();
                //peerId.ip = CurrentAnnouneResponse.peers[0].ip;
                //peerId.port = CurrentAnnouneResponse.peers[0].port;
                //for (var cnt01 = 0; cnt01 < 5; cnt01++)
                //{
                //    CurrentAnnouneResponse.peers.Add(peerId);
                //}

                CreateAndConnectPeers();

                if (RemotePeers.Count == 0)
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
        /// Download a torrent using an piece assembler per connected peer.
        /// </summary>
        /// <param name="progressFunction">User defined grogress function.</param>
        /// <param name="progressData">User defined grogress function data.</param>
        public void Download(ProgessCallBack progressFunction = null, Object progressData = null)
        {

            try
            {

                if (_mainTracker.Left > 0)
                {

                    List<Task> assembleTasks = new List<Task>();
                    CancellationTokenSource cancelAssemblerTaskSource = new CancellationTokenSource();

                    Program.Logger.Info("Starting torrent download for MetaInfo data ...");

                    _mainTracker.Event = Tracker.TrackerEvent.started;

                    foreach (var peer in RemotePeers)
                    {
                        assembleTasks.Add(Task.Run(() => AssemblePieces(peer, progressFunction, progressData, cancelAssemblerTaskSource)));
                    }

                    _fileToDownloader.Dc.CheckForMissingBlocksFromPeers();

                    Task.WaitAll(assembleTasks.ToArray());

                    if (!cancelAssemblerTaskSource.IsCancellationRequested)
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
        /// Load FileAgent asynchronously.
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
        /// Download torrent asynchronously.
        /// </summary>
        /// <param name="progressFunction">User defined grogress function.</param>
        /// <param name="progressData">User defined grogress function data.</param>
        public async Task DownloadAsync(ProgessCallBack progressFunction = null, Object progressData = null)
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
        /// Closedown FileAgent
        /// </summary>
        public void Close()
        {
            try
            {
                _mainTracker.StopAnnonncing();
                Program.Logger.Info("Closing peer socket.");
                foreach (var peer in RemotePeers)
                {
                    peer.Close();
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
        /// Start downloading torrent.
        /// </summary>
        public void Start()
        {
            try
            {
                Downloading.Set();
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
        /// Pause downloading torrent.
        /// </summary>
        public void Pause()
        {
            try
            {
                Downloading.Reset();
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
