//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: All the high level torrent control logic including download/upload
// of torrent pieces and updating the peers in the current swarm. Any  peers that
// are connected  have a piece assembler task created for them which puts together
// pieces that they request from the torrent (remote peer) before being written to disk.
//
// Copyright 2020.
//

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;

namespace BitTorrentLibrary
{

    /// <summary>
    /// Agent class definition.
    /// </summary>
    public class Agent
    {
        private readonly Manager _manager;                                       // Torrent context manager                      
                                                                                 //private readonly ConcurrentDictionary<string, TorrentContext> _torrents; // Torrents downloading/seeding
        private bool _agentRunning = false;                                      // == true while agent is up and running.
        private readonly HashSet<string> _deadPeers;                             // Dead peers list
        private readonly Assembler _pieceAssembler;                              // Piece assembler for agent
        private Socket _listenerSocket;                                          // Connection listener socket
        private readonly CancellationTokenSource _cancelTaskSource;              // Cancel all agent tasks
        private readonly AsyncQueue<PeerDetails> _peerSwarmQeue;                 // Queue of peers to add to swarm

        /// <summary>
        /// Start assembly task for connection with remote peer. If for any reason
        /// the connection fails then the peers ip is put into an dead peer list (hashset)
        /// so that no further connections are attempted.
        /// </summary>
        /// <param name="remotePeer"></param>
        private void StartPieceAssemblyTask(Peer remotePeer)
        {

            remotePeer.Connect(_manager);

            if (remotePeer.Connected)
            {
                if (!remotePeer.Tc.PeerSwarm.ContainsKey(remotePeer.Ip) && remotePeer.Tc.PeerSwarm.Count < remotePeer.Tc.MaximumSwarmSize)
                {
                    if (remotePeer.Tc.PeerSwarm.TryAdd(remotePeer.Ip, remotePeer))
                    {
                        Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                        remotePeer.AssemblerTask = Task.Run(() => _pieceAssembler.AssemblePieces(remotePeer));
                    }
                }
                if (remotePeer.AssemblerTask == null)
                {
                    remotePeer.Close();
                }
            }

            if (!remotePeer.Connected)
            {
                _deadPeers.Add(remotePeer.Ip);
                Log.Logger.Info($"Peer {remotePeer.Ip} added to dead peer list.");
            }
        }
        /// <summary>
        /// Inspects  peer queue, connects to the peer and creates piece assembler task 
        /// before adding to swarm.
        /// </summary>
        private async Task PeerConnectCreatorTaskAsync(CancellationToken cancelTask)
        {

            try
            {
                while (_agentRunning)
                {
                    PeerDetails peer = await _peerSwarmQeue.DequeueAsync(cancelTask);
                    try
                    {
                        if (_manager.Get(peer.infoHash, out TorrentContext tc))
                        {
                            // Only add peers that are not already there and is maximum swarm size hasnt been reached
                            if (!_deadPeers.Contains(peer.ip) && !tc.PeerSwarm.ContainsKey(peer.ip) && tc.PeerSwarm.Count < tc.MaximumSwarmSize)
                            {
                                StartPieceAssemblyTask(new Peer(peer.ip, peer.port, tc, null));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Log.Logger.Info($"Failed to connect to {peer.ip}.Added to dead per list.");
                        _deadPeers.Add(peer.ip);
                    }
                }
            }
            catch (Exception)
            {
                Log.Logger.Info("Remote peer connect creation task terminated.");
            }

        }
        /// <summary>
        /// Listen for remote peer connects and on success start peer task then add it to swarm.
        /// </summary>
        private async Task PeerListenCreatorTaskAsync(CancellationToken _)
        {

            try
            {

                _listenerSocket = PeerNetwork.GetListeningConnection();

                while (_agentRunning)
                {
                    Log.Logger.Info("Waiting for remote peer connect...");

                    Socket remotePeerSocket = await PeerNetwork.WaitForConnectionAsync(_listenerSocket);

                    if (_agentRunning)
                    {
                        Log.Logger.Info("Remote peer connected...");

                        var endPoint = PeerNetwork.GetConnectionEndPoint(remotePeerSocket);

                        // Pass in null torrent context as this is hooked up when we find the infohash from remote peer
                        StartPieceAssemblyTask(new Peer(endPoint.Item1, endPoint.Item2, null, remotePeerSocket));
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Agent) Error Error :" + ex.Message);
            }

            _listenerSocket?.Close();

            Log.Logger.Info("Remote Peer connect listener terminated.");

        }
        /// <summary>
        /// Setup data and resources needed by agent.
        /// </summary>
        /// <param name="manager">Torrent context manager</param>
        /// <param name="downloadPath">Download path.</param>
        public Agent(Manager manager, Assembler pieceAssembler)
        {
            _manager = manager;
            _pieceAssembler = pieceAssembler;
            _peerSwarmQeue = new AsyncQueue<PeerDetails>();
            _deadPeers = new HashSet<string>();
            _cancelTaskSource = new CancellationTokenSource();
            _deadPeers.Add("192.168.1.1"); // WITHOUT THIS HANGS (FOR ME)

        }
        /// <summary>
        /// 
        /// </summary>
        public void Startup()
        {
            _agentRunning = true;
            CancellationToken cancelTask = _cancelTaskSource.Token;
            Task.Run(() => Task.WaitAll(PeerConnectCreatorTaskAsync(cancelTask), PeerListenCreatorTaskAsync(cancelTask)));
        }

        /// <summary>
        /// Add torrent context to dictionary of running torrents.
        /// </summary>
        /// <param name="tc"></param>
        public void Add(TorrentContext tc)
        {
            try
            {
                _manager.Add(tc);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failed to add torrent context." + ex.Message);
            }

        }
        /// <summary>
        /// Remove torrent context from dictionary of running torrents
        /// </summary>
        /// <param name="tc"></param>
        public void Remove(TorrentContext tc)
        {

            try
            {
                _manager.Remove(tc);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failed to remove torrent context. " + ex.Message);
            }

        }
        /// <summary>
        /// Shutdown any agent running resourcses.
        /// </summary>
        public void ShutDown()
        {
            try
            {
                if (_agentRunning)
                {
                    _cancelTaskSource.Cancel();
                    foreach (var tc in _manager.GetTorrentList())
                    {
                        Close(tc);
                    }
                    PeerNetwork.ShutdownListener();
                    _agentRunning = false;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failed in shutdown." + ex.Message);
            }

        }
        /// <summary>
        /// Download a torrent using an piece assembler per connected peer.
        /// </summary>
        public void Download(TorrentContext tc)
        {
            try
            {
                if (tc.MainTracker.Left != 0)
                {
                    Log.Logger.Info("Starting torrent download for MetaInfo data ...");
                    tc.Status = TorrentStatus.Downloading;
                    tc.DownloadFinished.WaitOne();
                    tc.MainTracker.ChangeStatus(Tracker.TrackerEvent.completed);
                    Log.Logger.Info("Whole Torrent finished downloading.");
                }

                tc.Status = TorrentStatus.Seeding;

            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failed to download torrent file.");
            }
        }
        /// <summary>
        /// Download torrent asynchronously.
        /// </summary>
        public async Task DownloadAsync(TorrentContext tc)
        {
            try
            {
                await Task.Run(() => Download(tc)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : " + ex.Message);
            }
        }
        /// <summary>
        /// Closedown Agent
        /// </summary>
        public void Close(TorrentContext tc)
        {
            try
            {
                if (_agentRunning)
                {
                    tc.MainTracker.StopAnnouncing();
                    if (tc.PeerSwarm != null)
                    {
                        Log.Logger.Info("Closing peer sockets.");
                        foreach (var remotePeer in tc.PeerSwarm.Values)
                        {
                            remotePeer.Close();
                        }
                    }
                    tc.MainTracker.ChangeStatus(Tracker.TrackerEvent.stopped);
                    tc.Status = TorrentStatus.Ended;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : " + ex.Message);
            }
        }
        /// <summary>
        /// Start downloading torrent.
        /// </summary>
        public void Start(TorrentContext tc)
        {
            try
            {
                _pieceAssembler?.Paused.Set();
                tc.Status = TorrentStatus.Initialised;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : " + ex.Message);
            }
        }
        /// <summary>
        /// Pause downloading torrent.
        /// </summary>
        public void Pause(TorrentContext tc)
        {
            try
            {
                _pieceAssembler?.Paused.Reset();
                tc.Status = TorrentStatus.Paused;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : " + ex.Message);
            }
        }
        /// <summary>
        /// Return details about the currently torrents status.
        /// </summary>
        /// <returns></returns>
        public TorrentDetails GetTorrentDetails(TorrentContext tc)
        {

            return new TorrentDetails
            {
                fileName = tc.FileName,
                status = tc.Status,

                peers = (from peer in tc.PeerSwarm.Values
                         select new PeerDetails
                         {
                             ip = peer.Ip,
                             port = peer.Port
                         }).ToList(),

                downloadedBytes = tc.TotalBytesDownloaded,
                uploadedBytes = tc.TotalBytesUploaded,
                infoHash = tc.InfoHash,
                missingPiecesCount = tc.MissingPiecesCount,
                swarmSize = (UInt32)tc.PeerSwarm.Count,
                deadPeers = (UInt32)_deadPeers.Count
            };
        }
        /// <summary>
        /// Attach peer swarm queue to atart recieving peers.
        /// </summary>
        /// <param name="tracker"></param>
        public void AttachPeerSwarmQueue(Tracker tracker)
        {
            tracker._peerSwarmQueue = _peerSwarmQeue;
        }
        /// <summary>
        /// Detach peer swarm than queue.
        /// </summary>
        /// <param name="tracker"></param>
        public void DetachPeerSwarmQueu(Tracker tracker)
        {
            tracker._peerSwarmQueue = null;
        }
    }
}
