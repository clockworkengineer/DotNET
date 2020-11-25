using System.Collections.Concurrent;
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
    public class Agent
    {
        private readonly Manager _manager;                                 // Torrent context/dead peer manager
        private bool _agentRunning = false;                                // == true while agent is up and running.
        private readonly Assembler _pieceAssembler;                        // Piece assembler for agent
        private readonly CancellationTokenSource _cancelWorkerTaskSource;  // Cancel all agent worker tasks
        private readonly BlockingCollection<PeerDetails> _peerSwarmQeue;   // Queue of peers to add to swarm
        public bool Running { get => _agentRunning; }                      // == true then agent running
        /// <summary>
        /// Add Peer to swarm if it connected, is not already present in the swarm 
        /// and there is room enough.
        /// </summary>
        /// <param name="remotePeer"></param>
        private void AddPeerToSwarm(Peer remotePeer)
        {
            remotePeer.Handshake(_manager);
            if (!remotePeer.Connected ||
                !remotePeer.Tc.IsSpaceInSwarm(remotePeer.Ip) ||
                !remotePeer.Tc.peerSwarm.TryAdd(remotePeer.Ip, remotePeer))
            {
                throw new Exception($"Failure peer [{remotePeer.Ip}] not added to swarm.");
            }
            foreach (var pieceNumber in remotePeer.Tc.selector.LocalPieceSuggestions(remotePeer, 10))
            {
                PWP.Have(remotePeer, pieceNumber);
            }
            if (remotePeer.Tc.Status == TorrentStatus.Seeding)
            {
                PWP.Uninterested(remotePeer);
            }
            PWP.Unchoke(remotePeer);
            _manager.RemoFromDeadPeerList(remotePeer.Ip);
            Log.Logger.Info($"(Agent) Peer [{remotePeer.Ip}] added to swarm.");
        }
        /// <summary>
        /// Keep taking peers from queue added to be tracker,  try to connect to them and
        /// and on success add them to the active peer swarm.
        /// </summary>
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private void PeerConnectWorkerTask(CancellationToken cancelTask)
        {
            Log.Logger.Info("(Agent) Remote peer connect creation task started...");
            try
            {
                while (_agentRunning)
                {
                    try
                    {
                        PeerDetails peerDetails = _peerSwarmQeue.Take(cancelTask);
                        _manager.AddToDeadPeerList(peerDetails.ip);
                        PeerNetwork.Connect(new PeerConnector()
                        {
                            peerDetails = peerDetails,
                            callBack = AddConnectedToPeerToSpawn
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Debug($"BitTorrent (Agent) Error (Ignored): " + ex.Message);
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
        /// Called from asychronous connection listener when a peer connects to its port. 
        /// An attempt is then made to handshake with the peer and add it to the peer 
        /// swarm on success. On entry into the method we check a cancel source and throw
        /// an exception that will terminate the listener.
        /// </summary>
        /// <param name="remotePeerSocket"></param>
        private void AddRemotelyConnectingPeerToSpawn(Object obj)
        {
            _cancelWorkerTaskSource.Token.ThrowIfCancellationRequested();
            Peer remotePeer = null;
            try
            {
                Log.Logger.Info("(Agent) Remote peer connected...");
                PeerConnector connector = (PeerConnector)obj;
                connector.peerDetails = PeerNetwork.GetConnectingPeerDetails(connector.socket);
                _manager.AddToDeadPeerList(connector.peerDetails.ip);
                remotePeer = new Peer(connector.peerDetails.ip, connector.peerDetails.port, null, connector.socket);
                AddPeerToSwarm(remotePeer);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug($"BitTorrent (Agent) Error (Ignored): " + ex.Message);
                remotePeer?.Close();
            }
        }
        /// <summary>
        /// Called from asychronous connect handler on connection to a remote peer. 
        /// An attempt is then made to handshake with the peer and add it to the peer 
        /// swarm on success. On entry into the method we check a cancel source and throw
        /// an exception that will terminate the listener.
        /// </summary>
        /// <param name="remotePeerSocket"></param>
        private void AddConnectedToPeerToSpawn(Object obj)
        {
            _cancelWorkerTaskSource.Token.ThrowIfCancellationRequested();
            Peer remotePeer = null;
            try
            {
                PeerConnector connector = (PeerConnector)obj;
                if (_manager.GetTorrentContext(connector.peerDetails.infoHash, out TorrentContext tc))
                {
                    remotePeer = new Peer(connector.peerDetails.ip, connector.peerDetails.port, tc, connector.socket);
                    AddPeerToSwarm(remotePeer);
                }
                else
                {
                    connector.socket.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug($"BitTorrent (Agent) Error (Ignored): " + ex.Message);
                remotePeer?.Close();
            }
        }
        /// <summary>
        /// Setup data and resources needed by agent.
        /// </summary>
        /// <param name="manager">Torrent context manager</param>
        /// <param name="downloadPath">Download path.</param>
        /// <param name="listenPort"></param>
        public Agent(Manager manager, Assembler pieceAssembler, int listenPort = Host.DefaultPort)
        {
            PeerNetwork.listenPort = listenPort;
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _pieceAssembler = pieceAssembler ?? throw new ArgumentNullException(nameof(pieceAssembler));
            _peerSwarmQeue = new BlockingCollection<PeerDetails>();
            _cancelWorkerTaskSource = new CancellationTokenSource();
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
                    Task.Run(() => PeerConnectWorkerTask(_cancelWorkerTaskSource.Token));
                    PeerNetwork.StartListening(new PeerConnector
                    {
                        callBack = AddRemotelyConnectingPeerToSpawn
                    });
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
                _agentRunning = false;
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
                        tc.assemblyData.task = Task.Run(() => _pieceAssembler.AssemblePieces(tc));
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
                            remotePeer.Close();
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
                if (!_agentRunning)
                {
                    throw new Exception("Agent has not been started.");
                }
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
                        throw new Exception("The torrent is currently not in a running state.");
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
