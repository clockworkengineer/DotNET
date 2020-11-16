//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Class for adding peers to swarm and for the high level
// control of torrent contexts which includes the creation of their
// assembler task and for the starting of downloads/seeding.
//
// Copyright 2020.
//
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Sockets;
namespace BitTorrentLibrary
{
    /// <summary>
    /// Agent class definition.
    /// </summary>
    public class Agent
    {
        private readonly Manager _manager;                                 // Torrent context/dead peer manager
        private bool _agentRunning = false;                                // == true while agent is up and running.
        private readonly Assembler _pieceAssembler;                        // Piece assembler for agent
        private Socket _listenerSocket;                                    // Connection listener socket
        private readonly CancellationTokenSource _cancelWorkerTaskSource;  // Cancel all agent worker tasks
        private readonly AsyncQueue<PeerDetails> _peerSwarmQeue;           // Queue of peers to add to swarm
        private readonly AsyncQueue<Peer> _peerCloseQueue;                 // Peer close queue
        public bool Running { get => _agentRunning; }                      // == true then agent running
        /// <summary>
        /// Peer close processing task. Peers can be closed from differene threads and
        /// contexts and having a queue and only one place that they are closed solves
        /// any mutual exlusion issues.
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private async Task PeerCloseQueueTaskAsync(CancellationToken cancelTask)
        {
            Log.Logger.Info("(Agent) Peer close queue task started ... ");
            try
            {
                while (_agentRunning)
                {
                    Peer peer = await _peerCloseQueue.DequeueAsync(cancelTask);
                    peer.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Agent) Error :" + ex.Message);
            }
            Log.Logger.Info("(Agent) Close remaining peers left in queue... ");
            if (_peerCloseQueue.Count > 0)
            {
                for (UInt32 peerNo = 0; peerNo < _peerCloseQueue.Count; peerNo++)
                {
                    Peer peer = await _peerCloseQueue.DequeueAsync();
                    peer.Close();
                }
            }
            Log.Logger.Info("(Agent) Peer close queue task terminated.");
        }
        /// <summary>
        /// Add Peer to swarm if it connected, is not already present in the swarm 
        /// and there is room enough.
        /// </summary>
        /// <param name="remotePeer"></param>
        private void AddPeerToSwarm(Peer remotePeer)
        {
            remotePeer.Connect(_manager);
            if (remotePeer.Tc.IsSpaceInSwarm(remotePeer.Ip))
            {
                if (remotePeer.Tc.peerSwarm.TryAdd(remotePeer.Ip, remotePeer))
                {
                    remotePeer.BitfieldReceived.WaitOne();
                    foreach (var pieceNumber in remotePeer.Tc.selector.LocalPieceSuggestions(remotePeer, 10))
                    {
                        PWP.Have(remotePeer, pieceNumber);
                    }
                    if (remotePeer.Tc.Status == TorrentStatus.Seeding)
                    {
                        PWP.Uninterested(remotePeer);
                    }
                    PWP.Unchoke(remotePeer);
                    Log.Logger.Info($"(Agent) Peer [{remotePeer.Ip}] added to swarm.");
                    return;
                }
            }
            throw new BitTorrentException($"Failure peer [{remotePeer.Ip}] not added to swarm.");
        }
        /// <summary>
        /// Inspect peer queue added to by tracker, connect to the peer and add it to swarm
        /// on success.
        /// </summary>
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private async Task PeerConnectCreatorTaskAsync(CancellationToken cancelTask)
        {
            Log.Logger.Info("(Agent) Remote peer connect creation task started...");
            try
            {
                while (_agentRunning)
                {
                    Peer remotePeer = null;
                    PeerDetails peer = await _peerSwarmQeue.DequeueAsync(cancelTask);
                    try
                    {
                        if (_manager.GetTorrentContext(peer.infoHash, out TorrentContext tc))
                        {
                            remotePeer = new Peer(peer.ip, peer.port, tc, null)
                            {
                                peerCloseQueue = _peerCloseQueue
                            };
                            AddPeerToSwarm(remotePeer);
                        }
                    }
                    catch (SocketException ex)
                    {
                        if ((ex.ErrorCode == 111) || (ex.ErrorCode == 113) || (ex.ErrorCode == (int)SocketError.TimedOut))
                        {    // Connection refused    // No route to host
                            _manager.AddToDeadPeerList(peer.ip);
                            remotePeer?.QueueForClosure();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Debug($"BitTorrent (Agent) Error (Ignored): " + ex.Message);
                        remotePeer?.QueueForClosure();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Agent) Error :" + ex.Message);
            }
            Log.Logger.Info("(Agent) Remote peer connect creation task terminated.");
        }
        /// <summary>
        /// Listen for remote peer connects and on success add it to swarm. Note:
        /// that we pass in null for tc when creating the Peer as this is attached
        /// deeper down when we know what torrent (infohash) the remote client has
        /// sent so can find it.
        /// </summary>
        /// <param name="_"></param>
        /// <returns></returns>
        private async Task PeerListenCreatorTaskAsync(CancellationToken _)
        {
            Log.Logger.Info("(Agent) Remote Peer connect listener started...");
            try
            {
                _listenerSocket = PeerNetwork.GetListeningConnection();
                while (_agentRunning)
                {
                    Log.Logger.Info("(Agent) Waiting for remote peer connect...");
                    Peer remotePeer = null;
                    Socket remotePeerSocket = await PeerNetwork.WaitForConnectionAsync(_listenerSocket);
                    try
                    {
                        if (_agentRunning)
                        {
                            Log.Logger.Info("(Agent) Remote peer connected...");
                            PeerDetails peerDetails = PeerNetwork.GetConnectinPeerDetails(remotePeerSocket);
                            remotePeer = new Peer(peerDetails.ip, peerDetails.port, null, remotePeerSocket)
                            {
                                peerCloseQueue = _peerCloseQueue
                            };
                            AddPeerToSwarm(remotePeer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Debug($"BitTorrent (Agent) Error (Ignored): " + ex.Message);
                        remotePeer?.QueueForClosure();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Agent) Error :" + ex.Message);
            }
            _listenerSocket?.Close();
            Log.Logger.Info("(Agent) Remote Peer connect listener terminated.");
        }
        /// <summary>
        /// Setup data and resources needed by agent.
        /// </summary>
        /// <param name="manager">Torrent context manager</param>
        /// <param name="downloadPath">Download path.</param>
        public Agent(Manager manager, Assembler pieceAssembler)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _pieceAssembler = pieceAssembler ?? throw new ArgumentNullException(nameof(pieceAssembler));
            _peerSwarmQeue = new AsyncQueue<PeerDetails>();
            _cancelWorkerTaskSource = new CancellationTokenSource();
            _peerCloseQueue = new AsyncQueue<Peer>();
        }
        /// <summary>
        /// Startup worker tasks needed by the agent.
        /// </summary>
        public void Startup()
        {
            try
            {
                if (!_agentRunning)
                {
                    Log.Logger.Info("(Agent) Starting up Torrent Agent...");
                    _agentRunning = true;
                    Task.Run(() => PeerConnectCreatorTaskAsync(_cancelWorkerTaskSource.Token));
                    Task.Run(() => PeerListenCreatorTaskAsync(_cancelWorkerTaskSource.Token));
                    Task.Run(() => PeerCloseQueueTaskAsync(_cancelWorkerTaskSource.Token));
                    Log.Logger.Info("(Agent) Torrent Agent started.");
                }
                else
                {
                    throw new Exception("Agent is already running.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException("BitTorrent (Agent) Error : Failure to startup agent." + ex.Message);
            }
        }
        /// <summary>
        /// Add torrent context to managers database while creating an assembler task for it.
        /// </summary>
        /// <param name="tc"></param>
        public void AddTorrent(TorrentContext tc)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }
            try
            {
                if (_manager.AddTorrentContext(tc))
                {
                    if (tc.MainTracker != null)
                    {
                        tc.manager = _manager;
                        tc.assemblyData.task = Task.Run(() => _pieceAssembler.AssemblePieces(tc, tc.assemblyData.cancelTaskSource.Token));
                    }
                    else
                    {
                        throw new Exception("Torrent does not have a tracker associated with it.");
                    }
                }
                else
                {
                    throw new Exception("Torrent most probably has already been added.");
                }
            }
            catch (Exception ex)
            {
                throw new BitTorrentException("BitTorrent (Agent) Error : Failed to add torrent context." + ex.Message);
            }
        }
        /// <summary>
        /// Remove torrent context from managers database.
        /// </summary>
        /// <param name="tc"></param>
        public void RemoveTorrent(TorrentContext tc)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }
            try
            {
                if (!_manager.RemoveTorrentContext(tc))
                {
                    throw new Exception("It probably has been removed alrady or never added.");
                }
            }
            catch (Exception ex)
            {
                throw new BitTorrentException("BitTorrent (Agent) Error : Failed to remove torrent context." + ex.Message);
            }
        }
        /// <summary>
        /// Shutdown(close) any agent running resources used by the agent.
        /// </summary>
        public void ShutDown()
        {
            try
            {
                if (_agentRunning)
                {
                    Log.Logger.Info("(Agent) Shutting down torrent agent...");
                    _agentRunning = false;
                    foreach (var tc in _manager.TorrentList)
                    {
                        CloseTorrent(tc);
                    }
                    PeerNetwork.ShutdownListener();
                    _cancelWorkerTaskSource.Cancel();
                    Log.Logger.Info("(Agent) Torrent agent shutdown.");
                }
                else
                {
                    throw new Exception("Agent already shutdown.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException("BitTorrent (Agent) Error : Failed to shutdown agent." + ex.Message);
            }
        }
        /// <summary>
        /// Closedown torrent.
        /// </summary>
        public void CloseTorrent(TorrentContext tc)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }
            try
            {
                if (_manager.GetTorrentContext(tc.infoHash, out TorrentContext _))
                {
                    Log.Logger.Info($"(Agent) Closing torrent context for {Util.InfoHashToString(tc.infoHash)}.");
                    tc.assemblyData.cancelTaskSource.Cancel();
                    tc.MainTracker.ChangeStatus(TrackerEvent.stopped);
                    tc.MainTracker.StopAnnouncing();
                    if (tc.peerSwarm != null)
                    {
                        Log.Logger.Info("(Agent) Closing peer sockets.");
                        foreach (var remotePeer in tc.peerSwarm.Values)
                        {
                            remotePeer.QueueForClosure();
                        }
                    }
                    tc.Status = TorrentStatus.Ended;
                    Log.Logger.Info($"(Agent) Torrent context for {Util.InfoHashToString(tc.infoHash)} closed.");
                }
                else
                {
                    throw new Exception("Torrent hasnt been added to agent or may already have been closed.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException("BitTorrent (Agent) Error : Failure to close torrent context." + ex.Message);
            }
        }
        /// <summary>
        /// Start processing torrent; this many be to start downloading or seeding if it has already been
        /// sucessfully downloaded.
        /// </summary>
        public void StartTorrent(TorrentContext tc)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }
            try
            {
                if (_manager.GetTorrentContext(tc.infoHash, out TorrentContext _))
                {
                    tc.paused.Set();
                    if (tc.MainTracker.Left > 0)
                    {
                        tc.Status = TorrentStatus.Downloading;
                    }
                    else
                    {
                        tc.Status = TorrentStatus.Seeding;
                    }
                }
                else
                {
                    throw new Exception("Torrent hasnt been added to agent.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException("BitTorrent (Agent) Error : Failure to start torrent context." + ex.Message);
            }
        }
        /// <summary>
        /// Pause processing torrent.
        /// </summary>
        public void PauseTorrent(TorrentContext tc)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }
            try
            {
                if (_manager.GetTorrentContext(tc.infoHash, out TorrentContext _))
                {
                    if (tc.Status == TorrentStatus.Downloading || (tc.Status == TorrentStatus.Seeding))
                    {
                        tc.Status = TorrentStatus.Paused;
                        tc.paused.Reset();
                    }
                    else
                    {
                        throw new BitTorrentException("The torrent is currentlu not in a running state.");
                    }
                }
                else
                {
                    throw new Exception("Torrent hasnt been added to agent.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException("BitTorrent (Agent) Error : Failure to pause torrent context." + ex.Message);
            }
        }
        /// <summary>
        /// Return details about the torrents status.
        /// </summary>
        /// <returns></returns>
        public TorrentDetails GetTorrentDetails(TorrentContext tc)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }
            try
            {
                if (_manager.GetTorrentContext(tc.infoHash, out TorrentContext _))
                {
                    return new TorrentDetails
                    {
                        fileName = tc.FileName,
                        status = tc.Status,
                        peers = (from peer in tc.peerSwarm.Values
                                 select new PeerDetails
                                 {
                                     ip = peer.Ip,
                                     port = peer.Port
                                 }).ToList(),
                        downloadedBytes = tc.TotalBytesDownloaded,
                        uploadedBytes = tc.TotalBytesUploaded,
                        infoHash = tc.infoHash,
                        missingPiecesCount = tc.missingPiecesCount,
                        swarmSize = tc.peerSwarm.Count,
                        deadPeers = _manager.DeadPeerCount,
                        trackerStatus = tc.MainTracker.trackerStatus,
                        trackerStatusMessage = tc.MainTracker.lastResponse.statusMessage
                    };
                }
                else
                {
                    throw new Exception("Torrent hasnt been added to agent.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException("BitTorrent (Agent) Error : Failure to get torrent details." + ex.Message);
            }
        }
        /// <summary>
        /// Attach peer swarm queue to start recieving peers.
        /// </summary>
        /// <param name="tracker"></param>
        public void AttachPeerSwarmQueue(Tracker tracker)
        {
            tracker.peerSwarmQueue = _peerSwarmQeue;
        }
        /// <summary>
        /// Detach peer swarm than queue.
        /// </summary>
        /// <param name="tracker"></param>
        public void DetachPeerSwarmQueue(Tracker tracker)
        {
            tracker.peerSwarmQueue = null;
        }
    }
}
