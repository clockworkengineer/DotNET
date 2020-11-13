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

        /// <summary>
        /// Peer close processing task. Peers can be closed from differene threads and
        /// contexts and having a queue and only one place that they are closed solves
        /// any mutual exlusion issues.
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private async Task PeerCloseQueueTaskAsync(CancellationToken cancelTask)
        {

            Log.Logger.Info("Peer close queue task started ... ");

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

            Log.Logger.Info("Close remaining peers left in queue... ");
            if (_peerCloseQueue.Count > 0)
            {
                for (UInt32 peerNo = 0; peerNo < _peerCloseQueue.Count; peerNo++)
                {
                    Peer peer = await _peerCloseQueue.DequeueAsync();
                    peer.Close();
                }
            }

            Log.Logger.Info("Peer close queue task terminated.");

        }
        /// <summary>
        /// Add Peer to swarm if it connected, is not already present in the swarm 
        /// and there is room enough.
        /// </summary>
        /// <param name="remotePeer"></param>
        private void AddPeerToSwarm(Peer remotePeer)
        {

            remotePeer.peerCloseQueue = _peerCloseQueue;

            remotePeer.Connect(_manager);

            if (remotePeer.Connected)
            {
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
                        Log.Logger.Info($"Peer [{remotePeer.Ip}] added to swarm.");
                        return;
                    }
                }
            }

            throw new Error($"Peer [{remotePeer.Ip}] not added to swarm.");

        }
        /// <summary>
        /// Inspect peer queue added to by tracker, connect to the peer and add it to swarm
        /// on success.
        /// </summary>
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private async Task PeerConnectCreatorTaskAsync(CancellationToken cancelTask)
        {

            Log.Logger.Info("Remote peer connect creation task started...");

            try
            {
                while (_agentRunning)
                {
                    Peer remotePeer = null;
                    PeerDetails peer = await _peerSwarmQeue.DequeueAsync(cancelTask);
                    try
                    {
                        if (!_manager.IsPeerDead(peer.ip) && _manager.GetTorrentContext(peer.infoHash, out TorrentContext tc))
                        {
                            remotePeer = new Peer(peer.ip, peer.port, tc, null);
                            AddPeerToSwarm(remotePeer);
                        }
                    }
                    catch (SocketException ex)
                    {
                        if ((ex.ErrorCode == 111) || (ex.ErrorCode == 113))
                        {    // Connection refused    // No route to host
                            _manager.AddToDeadPeerList(peer.ip);
                            remotePeer.QueueForClosure();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Debug($"PeerConnectCreatorTaskAsync Error (Ignored): " + ex.Message);
                        remotePeer.QueueForClosure();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Agent) Error :" + ex.Message);
            }

            Log.Logger.Info("Remote peer connect creation task terminated.");

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

            Log.Logger.Info("Remote Peer connect listener started...");

            try
            {
                _listenerSocket = PeerNetwork.GetListeningConnection();

                while (_agentRunning)
                {
                    Peer remotePeer = null;

                    Log.Logger.Info("Waiting for remote peer connect...");

                    Socket remotePeerSocket = await PeerNetwork.WaitForConnectionAsync(_listenerSocket);

                    try
                    {
                        if (_agentRunning)
                        {
                            Log.Logger.Info("Remote peer connected...");
                            var endPoint = PeerNetwork.GetConnectionEndPoint(remotePeerSocket);
                            if (!_manager.IsPeerDead(endPoint.Item1))
                            {
                                remotePeer = new Peer(endPoint.Item1, endPoint.Item2, null, remotePeerSocket);
                                AddPeerToSwarm(remotePeer);
                            }
                            else
                            {
                                throw new Error("Peer not added to swarm as in dead list.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Debug($"PeerListenCreatorTaskAsync Error (Ignored): " + ex.Message);
                        remotePeer?.QueueForClosure();
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Agent) Error :" + ex.Message);
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
                Log.Logger.Info("Starting up Torrent Agent...");
                _agentRunning = true;
                Task.Run(() => PeerConnectCreatorTaskAsync(_cancelWorkerTaskSource.Token));
                Task.Run(() => PeerListenCreatorTaskAsync(_cancelWorkerTaskSource.Token));
                Task.Run(() => PeerCloseQueueTaskAsync(_cancelWorkerTaskSource.Token));
                Log.Logger.Info("Torrent Agent started.");
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failure to startup agent." + ex.Message);
            }
        }
        /// <summary>
        /// Add torrent context to managers database while creating an assembler task for it.
        /// </summary>
        /// <param name="tc"></param>
        public void AddTorrent(TorrentContext tc)
        {
            if (_manager.AddTorrentContext(tc))
            {
                tc.assemblerTask = Task.Run(() => _pieceAssembler.AssemblePieces(tc, tc.cancelAssemblerTaskSource.Token));
            }
            else
            {
                throw new Error("BitTorrent (Agent) Error : Failed to add torrent context.");
            }
        }
        /// <summary>
        /// Remove torrent context from managers database.
        /// </summary>
        /// <param name="tc"></param>
        public void RemoveTorrent(TorrentContext tc)
        {
            if (!_manager.RemoveTorrentContext(tc))
            {
                throw new Error("BitTorrent (Agent) Error : Failed to remove torrent context. ");
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
                    foreach (var tc in _manager.TorrentList)
                    {
                        CloseTorrent(tc);
                    }
                    PeerNetwork.ShutdownListener();
                    _cancelWorkerTaskSource.Cancel();
                    Log.Logger.Info("Torrent agent shutdown.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failed to shutdown agent." + ex.Message);
            }

        }
        /// <summary>
        /// Closedown torrent.
        /// </summary>
        public void CloseTorrent(TorrentContext tc)
        {
            try
            {
                Log.Logger.Info($"Closing torrent context for {Util.InfoHashToString(tc.infoHash)}.");
                tc.cancelAssemblerTaskSource.Cancel();
                tc.MainTracker.ChangeStatus(TrackerEvent.stopped);
                tc.MainTracker.StopAnnouncing();
                if (tc.peerSwarm != null)
                {
                    Log.Logger.Info("Closing peer sockets.");
                    foreach (var remotePeer in tc.peerSwarm.Values)
                    {
                        remotePeer.QueueForClosure();
                    }
                }
                tc.Status = TorrentStatus.Ended;
                Log.Logger.Info($"Torrent context for {Util.InfoHashToString(tc.infoHash)} closed.");

            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failure to close torrent context" + ex.Message);
            }
        }
        /// <summary>
        /// Start processing torrent; this many be to start downloading or seeding if it has already been
        /// sucessfully downloaded.
        /// </summary>
        public void StartTorrent(TorrentContext tc)
        {
            try
            {
                tc.paused.Set();
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failure to start torrent context." + ex.Message);
            }
        }
        /// <summary>
        /// Pause processing torrent.
        /// </summary>
        public void PauseTorrent(TorrentContext tc)
        {
            try
            {
                tc.Status = TorrentStatus.Paused;
                tc.paused.Reset();
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Agent) Error : Failure to pause torrent context." + ex.Message);
            }
        }
        /// <summary>
        /// Return details about the torrents status.
        /// </summary>
        /// <returns></returns>
        public TorrentDetails GetTorrentDetails(TorrentContext tc)
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
                swarmSize = (UInt32)tc.peerSwarm.Count,
                deadPeers = (UInt32)_manager.DeadPeerCount,
                trackerStatus = tc.MainTracker.trackerStatus,
                trackerStatusMessage = tc.MainTracker.lastResponse.statusMessage
            };
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
