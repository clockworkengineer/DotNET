﻿//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: All the high level torrent processing including download and upload.
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
        private readonly Tracker _mainTracker;                       // Main torrent tracker
        private readonly HashSet<string> _deadPeersList;             // Peers that failed to connect
        public Downloader TorrentDownloader { get; set; }            // Downloader for torrent
        public Dictionary<string, Peer> RemotePeers { get; set; }    // Connected remote peers
        public ManualResetEvent Downloading { get; set; }            // WaitOn when downloads == false
        public byte[] InfoHash { get; }                              // Torrent info hash
        public string TrackerURL { get; }                            // Main Tracker URL

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
                Log.Logger.Debug($"Running block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                CancellationToken cancelAssemblerTask = cancelAssemblerTaskSource.Token;

                PWP.Unchoke(remotePeer);

                remotePeer.BitfieldReceived.WaitOne();

                remotePeer.PeerChoking.WaitOne();

                while (_mainTracker.Left != 0)
                {
                    for (UInt32 nextPiece = 0; TorrentDownloader.SelectNextPiece(remotePeer, ref nextPiece);)
                    {
                        Log.Logger.Debug($"Assembling blocks for piece {nextPiece}.");

                        remotePeer.TransferingPiece = nextPiece;

                        UInt32 blockNumber = 0;
                        for (; !remotePeer.TorrentDownloader.Dc.IsBlockPieceLast(nextPiece, blockNumber); blockNumber++)
                        {
                            PWP.Request(remotePeer, nextPiece, blockNumber * Constants.BlockSize, Constants.BlockSize);
                        }

                        PWP.Request(remotePeer, nextPiece, blockNumber * Constants.BlockSize,
                                     TorrentDownloader.Dc.pieceMap[nextPiece].lastBlockLength);

                        remotePeer.WaitForPieceAssembly.WaitOne();
                        remotePeer.WaitForPieceAssembly.Reset();

                        if (remotePeer.TransferingPiece != -1)
                        {
                            remotePeer.TransferingPiece = -1;

                            Log.Logger.Debug($"All blocks for piece {nextPiece} received");

                            PieceBuffer pieceBuffer = new PieceBuffer(remotePeer.AssembledPiece);
                            TorrentDownloader.Dc.pieceBufferWriteQueue.Add(pieceBuffer);

                            _mainTracker.Left = TorrentDownloader.Dc.totalBytesToDownload - TorrentDownloader.Dc.totalBytesDownloaded;
                            _mainTracker.Downloaded = TorrentDownloader.Dc.totalBytesDownloaded;
                        } else {
                            throw new Error("Data lost.");
                        }

                        progressFunction?.Invoke(progressData);

                        if (cancelAssemblerTask.IsCancellationRequested)
                        {
                            foreach (var peer in RemotePeers.Values)
                            {
                                if (!peer.PeerChoking.WaitOne(0))
                                {
                                    peer.PeerChoking.Set();
                                }
                            }
                            return;
                        }

                        Log.Logger.Info((TorrentDownloader.Dc.totalBytesDownloaded / (double)TorrentDownloader.Dc.totalBytesToDownload).ToString("0.00%"));

                        Downloading.WaitOne();

                        remotePeer.PeerChoking.WaitOne();
                    }

                    foreach (var peer in RemotePeers.Values)
                    {
                        if (!peer.PeerChoking.WaitOne(0))
                        {
                            peer.PeerChoking.Set();
                        }
                    }
                }

                Log.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");
            }
            catch (Error ex)
            {
                Log.Logger.Error(ex.Message);
                cancelAssemblerTaskSource.Cancel();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                cancelAssemblerTaskSource.Cancel();
            }
        }

        /// <summary>
        /// Initializes a new instance of the FileAgent class.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public FileAgent(MetaInfoFile torrentFile, String downloadPath)
        {
            TorrentDownloader = new Downloader(torrentFile, downloadPath);
            Downloading = new ManualResetEvent(true);
            RemotePeers = new Dictionary<string, Peer>();
            _deadPeersList = new HashSet<string>();
            InfoHash =  torrentFile.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentFile.MetaInfoDict["announce"]);
            _mainTracker = new Tracker(this);

        }

        /// <summary>
        /// Connect peers and add to swarm on success.
        /// </summary>
        public void ConnectPeersAndAddToSwarm(AnnounceResponse response)
        {
            Log.Logger.Info("Connecting any new peers to swarm ....");

            foreach (var peer in response.peers)
            {
                try
                {
                    if (!RemotePeers.ContainsKey(peer.ip) && !_deadPeersList.Contains(peer.ip))
                    {
                        Peer remotePeer = new Peer(TorrentDownloader, peer.ip, peer.port, InfoHash);
                        remotePeer.Connect();
                        if (remotePeer.Connected)
                        {
                            RemotePeers.Add(remotePeer.Ip, remotePeer);
                            Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                        }
                    }
                }
                catch (Exception)
                {
                    Log.Logger.Info($"Failed to connect to {peer.ip}");
                    _deadPeersList.Add(peer.ip);
                }
            }

            Log.Logger.Info($"Number of peers in swarm  {RemotePeers.Count}");
        }

        /// <summary>
        /// Startup file agent.This includes starting announces to main tracker, building pieces
        /// to download map and getting and building the initial peer swarm/dead list.
        /// </summary>
        ///
        public void Startup()
        {
            try
            {
                Log.Logger.Info("Determine which pieces of file need to be downloaded still...");

                TorrentDownloader.BuildDownloadedPiecesMap();

                _mainTracker.Left = TorrentDownloader.Dc.totalBytesToDownload - TorrentDownloader.Dc.totalBytesDownloaded;
                if (_mainTracker.Left == 0)
                {
                    Log.Logger.Info("Torrent file fully downloaded already.");
                    return;
                }

                Log.Logger.Info("Initial main tracker announce ...");

                ConnectPeersAndAddToSwarm(_mainTracker.Announce());

                _mainTracker.StartAnnouncing();

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (FileAgent): Failed to load File Agent.");
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

                    Log.Logger.Info("Starting torrent download for MetaInfo data ...");

                    _mainTracker.Event = Tracker.TrackerEvent.started;

                    foreach (var peer in RemotePeers.Values)
                    {
                        assembleTasks.Add(Task.Run(() => AssemblePieces(peer, progressFunction, progressData, cancelAssemblerTaskSource)));
                    }

                    TorrentDownloader.Dc.CheckForMissingBlocksFromPeers();

                    Task.WaitAll(assembleTasks.ToArray());

                    if (!cancelAssemblerTaskSource.IsCancellationRequested)
                    {
                        _mainTracker.Event = Tracker.TrackerEvent.completed;
                        Log.Logger.Info("Whole Torrent finished downloading.");
                    }
                    else
                    {
                        Log.Logger.Info("Download aborted.");
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
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (FileAgent): Failed to download torrent file.");
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
                await Task.Run(() => Startup()).ConfigureAwait(false);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (FileAgent): " + ex.Message);
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
                await Task.Run(() => Download(progressFunction, progressData)).ConfigureAwait(false);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (FileAgent): " + ex.Message);
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
                if (RemotePeers != null)
                {
                    Log.Logger.Info("Closing peer sockets.");
                    foreach (var peer in RemotePeers.Values)
                    {
                        peer.Close();
                    }
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (FileAgent): " + ex.Message);
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
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (FileAgent): " + ex.Message);
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
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (FileAgent): " + ex.Message);
            }
        }
    }
}
