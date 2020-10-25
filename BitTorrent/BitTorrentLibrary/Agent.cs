//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: All the high level torrent processing including download/upload
// of torrent pieces and updating the peers in the current swarm. 
//
// Copyright 2020.
//

using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;

namespace BitTorrentLibrary
{

    public interface IAgent
    {
        void Close();
        void Download();
        Task DownloadAsync();
        TorrentDetails GetTorrentDetails();
        void Pause();
        void SetMainTracker(Tracker tracker);
        void Start();
        void UpdatePeerSwarmQueue(List<PeerDetails> peers);
    }

    /// <summary>
    /// Agent class definition.
    /// </summary>
    public class Agent : IAgent
    {
        private TorrentStatus _status;                                     // Current torrent status
        private bool _agentRunning = false;                                // true thile agent is up and running.
        private readonly DownloadContext _dc;                              // Torrent download context
        private readonly BlockingCollection<PeerDetails> _peersTooSwarm;   // Peers to add to swarm queue
        private HashSet<string> _deadPeers;                                // Dead peers
        private readonly Task _peerConnectCreatorTask;                     // Peer swarm creator task
        private readonly Task _peerListenCreatorTask;                      // Peer swarm peer connect creator task
        private readonly Assembler _pieceAssembler;                        // Piece assembler for agent
        private Socket _listenerSocket;                                    // Connection listener socket
        private Tracker _mainTracker;                                      // Main torrent tracker

        /// <summary>
        /// Display peer task statistics.
        /// </summary>
        private void DisplayStats()
        {
            int peersChoking = 0;

            foreach (var peer in _dc.PeerSwarm.Values)
            {
                if (!peer.PeerChoking.WaitOne(0))
                {
                    peersChoking++;
                }
            }
            Log.Logger.Info($"%[Peers Choking {peersChoking}] [Missing Piece Count {_dc.MissingPiecesCount}] " +
            $"[Number of peers in swarm  {_dc.PeerSwarm.Count}/{_mainTracker.MaximumSwarmSize}]");
        }
        /// <summary>
        /// Inspects  peer queue, connects to the peer and creates piece assembler task before adding to swarm.
        /// </summary>
        private void PeerConnectCreatorTask()
        {

            while (!_peersTooSwarm.IsCompleted && _agentRunning)
            {
                PeerDetails peer = _peersTooSwarm.Take();
                try
                {
                    // Only add peers that are not already there and is maximum swarm size hasnt been reached
                    if (_deadPeers.Contains(peer.ip) || _dc.PeerSwarm.ContainsKey(peer.ip) || _dc.PeerSwarm.Count >= _mainTracker.MaximumSwarmSize)
                    {
                        continue;
                    }
                    Peer remotePeer = new Peer(peer.ip, peer.port, _dc);
                    remotePeer.Connect();
                    if (remotePeer.Connected)
                    {

                        if (_dc.PeerSwarm.TryAdd(remotePeer.Ip, remotePeer))
                        {
                            Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                            remotePeer.AssemblerTask = Task.Run(() => _pieceAssembler.AssemblePieces(remotePeer));
                        }
                        else
                        {
                            remotePeer.Close();
                        }
                    }
                    else
                    {
                        _deadPeers.Add(peer.ip);
                    }

                }
                catch (Exception)
                {
                    Log.Logger.Info($"Failed to connect to {peer.ip}");
                    _deadPeers.Add(peer.ip);
                }
            }

        }
        /// <summary>
        /// Listen for remote peer connects and on success start peer task then add it o swarm.
        /// </summary>
        private void PeerListenCreatorTask()
        {

            try
            {

                _listenerSocket = PeerNetwork.GetListeningConnection();

                while (true)
                {
                    Log.Logger.Info("Waiting for remote peer connect...");

                    Socket remotePeerSocket = PeerNetwork.WaitForConnection(_listenerSocket);

                    if (!_agentRunning)
                    {
                        break;
                    }

                    Log.Logger.Info("Remote peer connected...");

                    var endPoint = PeerNetwork.GetConnectionEndPoint(remotePeerSocket);

                    if (endPoint.Item1 == "192.168.1.1")
                    { // NO IDEA WHATS BEHIND THIS AT PRESENT (HANGS IF WE DONT CLOSE THIS)
                        remotePeerSocket.Close();
                        continue;
                    }

                    // Only add peers that are not already there and is maximum swarm size hasnt been reached
                    if (_dc.PeerSwarm.ContainsKey(endPoint.Item1) || _dc.PeerSwarm.Count >= _mainTracker.MaximumSwarmSize)
                    {
                        continue;
                    }

                    Log.Logger.Info($"++Remote peer IP = {endPoint.Item1}:{endPoint.Item2}.");

                    Peer remotePeer = new Peer(endPoint.Item1, endPoint.Item2, _dc, remotePeerSocket);
                    remotePeer.Accept();
                    if (remotePeer.Connected)
                    {
                        _dc.PeerSwarm.TryAdd(remotePeer.Ip, remotePeer);
                        Log.Logger.Info($"++BTP: Local Peer [{ PeerID.Get()}] from remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                        remotePeer.AssemblerTask = Task.Run(() => _pieceAssembler.AssemblePieces(remotePeer));
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Agent) Error :" + ex.Message);
            }

            _listenerSocket.Close();

            Log.Logger.Info("Remote Peer connect listener terminated.");

        }
        /// <summary>
        /// Setup data and resources needed by agent.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public Agent(DownloadContext dc, Assembler pieceAssembler = null)
        {
            _dc = dc;
            _pieceAssembler = pieceAssembler; ;
            _peersTooSwarm = new BlockingCollection<PeerDetails>();
            _deadPeers = new HashSet<string>();
            _peerListenCreatorTask = Task.Run(() => PeerListenCreatorTask());
            _peerConnectCreatorTask = Task.Run(() => PeerConnectCreatorTask());
            _agentRunning = true;

        }
        ~Agent()
        {
            _peersTooSwarm.CompleteAdding();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tracker"></param>
        public void SetMainTracker(Tracker tracker)
        {
            _mainTracker = tracker;
        }
        /// <summary>
        /// Add peers to swarm creation queue.
        /// </summary>
        /// <param name="peers"></param>
        public void UpdatePeerSwarmQueue(List<PeerDetails> peers)
        {
            if (peers != null)
            {

                Log.Logger.Info("Queuing new peers for swarm ....");

                foreach (var peerDetails in peers)
                {
                    _peersTooSwarm.Add(peerDetails);
                }

                _mainTracker.NumWanted = Math.Max(_mainTracker.MaximumSwarmSize - _dc.PeerSwarm.Count, 0);

            }

            DisplayStats();

        }
        /// <summary>
        /// Download a torrent using an piece assembler per connected peer.
        /// </summary>
        public void Download()
        {
            try
            {
                if (_mainTracker.Left != 0)
                {
                    _status = TorrentStatus.Downloading;

                    Log.Logger.Info("Starting torrent download for MetaInfo data ...");

                    _dc.DownloadFinished.WaitOne();

                    _mainTracker.ChangeStatus(Tracker.TrackerEvent.completed);

                    Log.Logger.Info("Whole Torrent finished downloading.");

                    _status = TorrentStatus.Uploading;
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
                if (_agentRunning)
                {
                    _agentRunning = false;

                    _mainTracker.StopAnnouncing();
                    if (_dc.PeerSwarm != null)
                    {
                        Log.Logger.Info("Closing peer sockets.");
                        foreach (var remotePeer in _dc.PeerSwarm.Values)
                        {
                            remotePeer.Close();
                        }
                    }
                    _mainTracker.ChangeStatus(Tracker.TrackerEvent.stopped);

                    PeerNetwork.ShutdownListener();

                    _status = TorrentStatus.Stopped;

                }
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
                _pieceAssembler?.Paused.Set();
                _status = TorrentStatus.Started;
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
                _pieceAssembler?.Paused.Reset();
                _status = TorrentStatus.Paused;

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
        /// Return details about the currently torrents status.
        /// </summary>
        /// <returns></returns>
        public TorrentDetails GetTorrentDetails()
        {

            return new TorrentDetails
            {
                status = _status,

                peers = (from peer in _dc.PeerSwarm.Values
                         select new PeerDetails
                         {
                             ip = peer.Ip,
                             port = peer.Port
                         }).ToList(),

                downloadedBytes = _dc.TotalBytesDownloaded,
                uploadedBytes = _dc.TotalBytesUploaded,
                infoHash = _dc.InfoHash,
                missingPiecesCount = _dc.MissingPiecesCount

            };
        }
    }
}
