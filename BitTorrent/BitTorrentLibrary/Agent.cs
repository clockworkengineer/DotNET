//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: All the high level torrent processing including download and upload
// of files.
//
// Copyright 2019.
//

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace BitTorrentLibrary
{
    /// <summary>
    /// File Agent class definition.
    /// </summary>
    public class Agent
    {
        public delegate void ProgessCallBack(Object progressData);   // Download progress 
        private readonly CancellationTokenSource _cancelTaskSource;  // Cancellation token source
        private ProgessCallBack _progressFunction = null;            // Download progress function
        private Object _progressData = null;                         // Download progress function data
        private readonly HashSet<string> _deadPeersList;             // Peers that failed to connect
        private readonly ManualResetEvent _downloading;              // WaitOn when downloads == false
        private readonly Downloader _torrentDownloader;              // Downloader for torrent
        public Dictionary<string, Peer> RemotePeers { get; set; }    // Connected remote peers
        public byte[] InfoHash { get; }                              // Torrent info hash
        public string TrackerURL { get; }                            // Main Tracker URL
        public Tracker MainTracker { get; set; }                     // Main torrent tracker

        /// <summary>
        /// Stopping all peers so unchoke them all.
        /// </summary>
        private void UnblockAllChokingPeers()
        {
            _downloading.Set();
            foreach (var peer in from peer in RemotePeers.Values
                                 where !peer.PeerChoking.WaitOne(0)
                                 select peer)
            {
                peer.PeerChoking.Set();
                peer.BitfieldReceived.Set();
            }
        }
        /// <summary>
        /// Request piece number from remote peer.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        private void RequestPieceFromPeer(Peer remotePeer, uint pieceNumber)
        {
            UInt32 blockNumber = 0;
            for (; !remotePeer.TorrentDownloader.Dc.IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
            {
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize, Constants.BlockSize);
            }

            PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize,
                         _torrentDownloader.Dc.PieceMap[pieceNumber].lastBlockLength);
        }
        /// <summary>
        /// Assembles the pieces of a torrent block by block.A task is created using this method for each connected peer.
        /// </summary>
        /// <returns>Task reference on completion.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="progressFunction">Progress function.</param>
        /// <param name="progressData">Progress data.</param>
        private void AssemblePieces(Peer remotePeer, ProgessCallBack progressFunction, Object progressData, CancellationTokenSource cancelTaskSource)
        {
            try
            {
                Log.Logger.Debug($"Running block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                CancellationToken cancelTask = cancelTaskSource.Token;

                PWP.Unchoke(remotePeer);

                remotePeer.BitfieldReceived.WaitOne();
                remotePeer.PeerChoking.WaitOne();

                while (MainTracker.Left != 0)
                {
                    for (UInt32 nextPiece = 0; _torrentDownloader.SelectNextPiece(remotePeer, ref nextPiece);)
                    {
                        Log.Logger.Debug($"Assembling blocks for piece {nextPiece}.");

                        RequestPieceFromPeer(remotePeer, nextPiece);

                        remotePeer.WaitForPieceAssembly.WaitOne();
                        remotePeer.WaitForPieceAssembly.Reset();

                        Log.Logger.Debug($"All blocks for piece {nextPiece} received");

                        _torrentDownloader.Dc.PieceBufferWriteQueue.Add(new PieceBuffer(remotePeer.AssembledPiece));
                        MainTracker.Left = BytesLeftToDownload();
                        MainTracker.Downloaded = _torrentDownloader.Dc.TotalBytesDownloaded;                       // }

                        progressFunction?.Invoke(progressData);

                        if (cancelTask.IsCancellationRequested)
                        {
                            UnblockAllChokingPeers();
                            return;
                        }

                        Log.Logger.Info((_torrentDownloader.Dc.TotalBytesDownloaded / (double)_torrentDownloader.Dc.TotalBytesToDownload).ToString("0.00%"));

                        _downloading.WaitOne();
                        remotePeer.PeerChoking.WaitOne();
                    }

                    UnblockAllChokingPeers();

                }

                Log.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                cancelTaskSource.Cancel();
            }
        }
        /// <summary>
        /// Return the number of bytes left in a torrent to download.
        /// </summary>
        /// <returns></returns>
        public UInt64 BytesLeftToDownload()
        {
            return _torrentDownloader.Dc.TotalBytesToDownload - _torrentDownloader.Dc.TotalBytesDownloaded;
        }
        /// <summary>
        /// Initializes a new instance of a torrent agent.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public Agent(MetaInfoFile torrentFile, Downloader downloader)
        {
            _torrentDownloader = downloader;
            _torrentDownloader.BuildDownloadedPiecesMap();
            RemotePeers = new Dictionary<string, Peer>();
            InfoHash = torrentFile.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentFile.MetaInfoDict["announce"]);
            _deadPeersList = new HashSet<string>();
            _downloading = new ManualResetEvent(true);
            _cancelTaskSource = new CancellationTokenSource();
        }
        /// <summary>
        /// Connect peers and add to swarm on success.
        /// </summary>
        /// <param name="peers"></param>
        public void UpdatePeerSwarm(List<PeerDetails> peers)
        {
            if (peers != null)
            {
                Log.Logger.Info("Remove dead peers from swarm....");

                List<string> deadPeers = (from peer in RemotePeers.Values
                                          where !peer.Connected
                                          select peer.Ip).ToList();

                foreach (var ip in deadPeers)
                {
                    RemotePeers.Remove(ip);
                    _deadPeersList.Add(ip);
                    Log.Logger.Info($"Dead Peer {ip} removed from swarm.");
                }

                Log.Logger.Info("Connecting any new peers to swarm ....");

                foreach (var peer in peers)
                {
                    try
                    {
                        if (!RemotePeers.ContainsKey(peer.ip) && !_deadPeersList.Contains(peer.ip))
                        {
                            Peer remotePeer = new Peer(_torrentDownloader, peer.ip, peer.port, InfoHash);
                            remotePeer.Connect();
                            if (remotePeer.Connected)
                            {
                                RemotePeers.Add(remotePeer.Ip, remotePeer);
                                Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                            } else {
                                _deadPeersList.Add(peer.ip);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Log.Logger.Info($"Failed to connect to {peer.ip}");
                        _deadPeersList.Add(peer.ip);
                    }

                }
            }

            MainTracker.NumWanted = Math.Max(MainTracker.MaximumSwarmSize-RemotePeers.Count, 0);
            Log.Logger.Info($"Number of peers in swarm  {RemotePeers.Count}");
        }

        /// <summary>
        /// Set download progress function and data
        /// </summary>
        /// <param name="progressFunction">User defined grogress function.</param>
        /// <param name="progressData">User defined grogress function data.</param>
        public void Progress(ProgessCallBack progressFunction = null, Object progressData = null)
        {
            _progressFunction = progressFunction;
            _progressData = progressData;
        }

        /// <summary>
        /// Download a torrent using an piece assembler per connected peer.
        /// </summary>
        public void Download()
        {
            try
            {
                if (MainTracker.Left > 0)
                {
                    List<Task> assembleTasks = new List<Task>();

                    Log.Logger.Info("Starting torrent download for MetaInfo data ...");

                    MainTracker.ChangeStatus(Tracker.TrackerEvent.started);

                    foreach (var peer in RemotePeers.Values)
                    {
                        assembleTasks.Add(Task.Run(() => AssemblePieces(peer, _progressFunction, _progressData, _cancelTaskSource)));
                    }

                    Task.WaitAll(assembleTasks.ToArray());

                    if (!_cancelTaskSource.IsCancellationRequested)
                    {
                        MainTracker.ChangeStatus(Tracker.TrackerEvent.completed);
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
                throw new Error("BitTorrent Error (Agent): Failed to download torrent file.");
            }
        }

        /// <summary>
        /// Download torrent asynchronously.
        /// </summary>
        public async Task DownloadAsync()
        {
            try
            {
                await Task.Run(() => Download()).ConfigureAwait(false);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Agent): " + ex.Message);
            }
        }

        /// <summary>
        /// Closedown Agent
        /// </summary>
        public void Close()
        {
            try
            {
                MainTracker.StopAnnouncing();
                if (RemotePeers != null)
                {
                    Log.Logger.Info("Closing peer sockets.");
                    foreach (var peer in RemotePeers.Values)
                    {
                        peer.Close();
                    }
                }
                MainTracker.ChangeStatus(Tracker.TrackerEvent.stopped);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Agent): " + ex.Message);
            }
        }

        /// <summary>
        /// Start downloading torrent.
        /// </summary>
        public void Start()
        {
            try
            {
                _downloading.Set();
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Agent): " + ex.Message);
            }
        }

        /// <summary>
        /// Pause downloading torrent.
        /// </summary>
        public void Pause()
        {
            try
            {
                _downloading.Reset();
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Agent): " + ex.Message);
            }
        }
    }
}
