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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
namespace BitTorrentLibrary
{
    public class Agent
    {
        private readonly IAgentNetwork _network;                                 // Network layer used by agent
        private readonly Manager _manager;                                       // Torrent context/dead peer manager
        private bool _agentRunning = false;                                      // == true while agent is up and running.
        private readonly Assembler _pieceAssembler;                              // Piece assembler for agent
        private readonly CancellationTokenSource _cancelWorkerTaskSource;        // Cancel all agent worker tasks
        private readonly BlockingCollection<PeerDetails> _peerSwarmQeue;         // Queue of peers to add to swarm
        public bool Running { get => _agentRunning; }                            // == true then agent running
        public ICollection<TorrentContext> TorrentList => _manager.GetTorrentList();  // List of torrent contexts
        /// <summary>
        /// Add Peer to swarm if it connected, is not already present in the swarm 
        /// and there is room enough.
        /// </summary>
        /// <param name="remotePeer"></param>
        private void AddPeerToSwarm(Peer remotePeer)
        {
            remotePeer.Handshake(_manager);
            if (!remotePeer.Tc.IsSpaceInSwarm(remotePeer.Ip) ||
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
            _manager.RemoveFromDeadPeerList(remotePeer.Ip);
            Log.Logger.Info($"Peer [{remotePeer.Ip}] added to swarm.");
        }
        /// <summary>
        /// Keep taking peers from queue added to be tracker,  try to connect to them and
        /// and on success add them to the active peer swarm.
        /// </summary>
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private void PeerConnectWorkerTask(CancellationToken cancelTask)
        {
            Log.Logger.Info("Remote peer connect creation task started...");
            while (_agentRunning)
            {
                try
                {
                    PeerDetails peerDetails = _peerSwarmQeue.Take(cancelTask);
                    _manager.AddToDeadPeerList(peerDetails.ip);
                    _network.Connect(new AgentConnector()
                    {
                        peerDetails = peerDetails,
                        callBack = AddConnectedToPeerToSpawn
                    });
                }
                catch (Exception ex)
                {
                    Log.Logger.Error("Error (Ignored): " + ex.Message);
                }
            }
            Log.Logger.Info("Remote peer connect creation task terminated.");
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
            AgentConnector connector = (AgentConnector)obj;
            try
            {
                Log.Logger.Info("Remote peer connected...");
                connector.peerDetails = _network.GetConnectingPeerDetails(connector.socket);
                _manager.AddToDeadPeerList(connector.peerDetails.ip);
                remotePeer = new Peer(connector.peerDetails.ip, connector.peerDetails.port, connector.socket);
                connector.socket = null;    // Close socket in peer.Close()
                AddPeerToSwarm(remotePeer);
            }
            catch (Exception ex)
            {
                // Close peer (this includes its socket)
                // Note : If peer wasnt created we need to close raw socket
                Log.Logger.Error("Error (Ignored): " + ex.Message);
                remotePeer?.Close();
                connector.socket?.Close();
            }
        }
        /// <summary>
        /// Called from asychronous connect handler on connection to a remote peer. 
        /// An attempt is then made to handshake with the peer and add it to the peer 
        /// swarm on success.
        /// </summary>
        /// <param name="remotePeerSocket"></param>
        private void AddConnectedToPeerToSpawn(Object obj)
        {
            Peer remotePeer = null;
            AgentConnector connector = (AgentConnector)obj;
            try
            {
                if (_manager.GetTorrentContext(connector.peerDetails.infoHash, out TorrentContext tc))
                {
                    remotePeer = new Peer(connector.peerDetails.ip, connector.peerDetails.port, tc, connector.socket);
                    connector.socket = null; // Close socket in peer.Close()
                    AddPeerToSwarm(remotePeer);
                }
                else
                {
                    // Close raw socket as peer has not been created
                    connector.socket.Close();
                    connector.socket = null;
                }
            }
            catch (Exception ex)
            {
                // Close peer (this includes its socket)
                // Note : If peer wasnt created we need to close raw socket
                Log.Logger.Error("Error (Ignored): " + ex.Message);
                remotePeer?.Close();
                connector.socket?.Close();
            }
        }
        /// <summary>
        /// Setup data and resources needed by agent.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="pieceAssembler"></param>
        /// <param name="network"></param>
        /// <param name="listenPort"></param>
        /// <returns></returns>
        internal Agent(Manager manager, Assembler pieceAssembler, IAgentNetwork network, int listenPort = Host.DefaultPort) :
                     this(manager, pieceAssembler, listenPort)
        {
            _network = network;
        }
        /// <summary>
        /// Setup data and resources needed by agent.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="pieceAssembler"></param>
        /// <param name="listenPort"></param>
        public Agent(Manager manager, Assembler pieceAssembler, int listenPort = Host.DefaultPort)
        {
            AgentNetwork.listenPort = listenPort;
            _network = new AgentNetwork();
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
                    Log.Logger.Info("Starting up Torrent Agent...");
                    _agentRunning = true;
                    Task.Run(() => PeerConnectWorkerTask(_cancelWorkerTaskSource.Token));
                    _network.StartListening(new AgentConnector
                    {
                        callBack = AddRemotelyConnectingPeerToSpawn
                    });
                    Log.Logger.Info("Torrent Agent started.");
                }
                else
                {
                    throw new Exception("Agent is already running.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
                _agentRunning = false;
                throw new BitTorrentException("Failure to startup agent." + ex.Message);
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
                throw new BitTorrentException("Failed to add torrent context." + ex.Message);
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
                throw new BitTorrentException("Failed to remove torrent context." + ex.Message);
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
                    Log.Logger.Info("Shutting down torrent agent...");
                    _agentRunning = false;
                    foreach (var tc in _manager.GetTorrentList())
                    {
                        CloseTorrent(tc);
                    }
                    _network.ShutdownListener();
                    _cancelWorkerTaskSource.Cancel();
                    Log.Logger.Info("Torrent agent shutdown.");
                }
                else
                {
                    throw new Exception("Agent already shutdown.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
                throw new BitTorrentException("Failed to shutdown agent." + ex.Message);
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
                    Log.Logger.Info($"Closing torrent context for {Util.InfoHashToString(tc.infoHash)}.");
                    tc.assemblyData.cancelTaskSource.Cancel();
                    tc.MainTracker.ChangeStatus(TrackerEvent.stopped);
                    tc.MainTracker.StopAnnouncing();
                    if (tc.peerSwarm != null)
                    {
                        Log.Logger.Info("Closing peer sockets.");
                        foreach (var remotePeer in tc.peerSwarm.Values)
                        {
                            remotePeer.Close();
                        }
                    }
                    tc.Status = TorrentStatus.Ended;
                    Log.Logger.Info($"Torrent context for {Util.InfoHashToString(tc.infoHash)} closed.");
                }
                else
                {
                    throw new Exception("Torrent hasnt been added to agent or may already have been closed.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
                throw new BitTorrentException("Failure to close torrent context." + ex.Message);
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
                Log.Logger.Error(ex);
                throw new BitTorrentException("Failure to start torrent context." + ex.Message);
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
                Log.Logger.Error(ex);
                throw new BitTorrentException("Failure to pause torrent context." + ex.Message);
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
                        bytesPerSecond = tc.BytesPerSecond(),
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
                Log.Logger.Error(ex);
                throw new BitTorrentException("Failure to get torrent details." + ex.Message);
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
