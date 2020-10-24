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
    public delegate void ProgessCallBack(Object progressData);            // Download progress callback

    /// <summary>
    /// Agent class definition.
    /// </summary>
    public class Agent
    {
        private bool _agentRunning = false;                                // true thile agent is up and running.
        private DownloadContext _dc;
        private readonly BlockingCollection<PeerDetails> _peersTooSwarm;   // Peers to add to swarm queue
        private readonly Task _peerConnectCreatorTask;                     // Peer swarm creator task
        private readonly Task _peerListenCreatorTask;                      // Peer swarm peer connect creator task
        private readonly Downloader _torrentDownloader;                    // Downloader for torrent
        private readonly Assembler _pieceAssembler;                        // Piece assembler for agent
        private readonly ConcurrentDictionary<string, Peer> _peerSwarm;    // Connected remote peers in swarm
        private Socket _listenerSocket;
        public byte[] InfoHash { get; }                                    // Torrent info hash
        public string TrackerURL { get; }                                  // Main Tracker URL
        public Tracker MainTracker { get; set; }                           // Main torrent tracker
        public UInt64 Left => _dc.BytesLeftToDownload();                   // Number of bytes left to download; 

        /// <summary>
        /// Display peer task statistics.
        /// </summary>
        private void DisplayStats()
        {
            int peersChoking = 0;

            foreach (var peer in _peerSwarm.Values)
            {
                if (!peer.PeerChoking.WaitOne(0))
                {
                    peersChoking++;
                }
            }
            Log.Logger.Info($"%[Peers Choking {peersChoking}] [Missing Piece Count {_dc.PieceSelector.MissingPiecesCount()}] " +
            $"[Number of peers in swarm  {_peerSwarm.Count}/{MainTracker.MaximumSwarmSize}] [Active Downloaders {_pieceAssembler?.ActiveDownloaders}] " +
            $"[Active Uploaders {_pieceAssembler?.ActiveUploaders}]");
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
                    if (_peerSwarm.ContainsKey(peer.ip) || _peerSwarm.Count >= MainTracker.MaximumSwarmSize)
                    {
                        continue;
                    }
                    Peer remotePeer = new Peer(peer.ip, peer.port, InfoHash, _dc);
                    remotePeer.Connect();
                    if (remotePeer.Connected)
                    {

                        if (_peerSwarm.TryAdd(remotePeer.Ip, remotePeer))
                        {
                            Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                            remotePeer.AssemblerTask = Task.Run(() => _pieceAssembler.AssemblePieces(remotePeer));
                        }
                        else
                        {
                            remotePeer.Close();
                        }
                    }

                }
                catch (Exception)
                {
                    Log.Logger.Info($"Failed to connect to {peer.ip}");
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

                    if (!_agentRunning) {
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
                    if (_peerSwarm.ContainsKey(endPoint.Item1) || _peerSwarm.Count >= MainTracker.MaximumSwarmSize)
                    {
                        continue;
                    }

                    Log.Logger.Info($"++Remote peer IP = {endPoint.Item1}:{endPoint.Item2}.");

                    Peer remotePeer = new Peer(endPoint.Item1, endPoint.Item2, InfoHash, _dc, remotePeerSocket);
                    remotePeer.Accept();
                    if (remotePeer.Connected)
                    {
                        _peerSwarm.TryAdd(remotePeer.Ip, remotePeer);
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
            //_listenerSocket.Dispose();

            Log.Logger.Info("Remote Peer connect listener terminated.");

        }
        /// <summary>
        /// Setup data and resources needed by agent.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public Agent(MetaInfoFile torrentFile, DownloadContext dc, Assembler pieceAssembler = null)
        {
            _dc = dc;
            _pieceAssembler = pieceAssembler;
            _peerSwarm = new ConcurrentDictionary<string, Peer>();
            _peersTooSwarm = new BlockingCollection<PeerDetails>();
            InfoHash = torrentFile.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentFile.MetaInfoDict["announce"]);
            _peerListenCreatorTask = Task.Run(() => PeerListenCreatorTask());
            _peerConnectCreatorTask = Task.Run(() => PeerConnectCreatorTask());
            _agentRunning = true;

        }
        ~Agent()
        {
            _peersTooSwarm.CompleteAdding();
        }
        /// <summary>
        /// Add peers to swarm creation queue.
        /// </summary>
        /// <param name="peers"></param>
        public void UpdatePeerSwarmQueue(List<PeerDetails> peers)
        {
            if (peers != null)
            {
                Log.Logger.Info("Remove dead peers from swarm....");

                List<string> deadPeers = (from peer in _peerSwarm.Values
                                          where !peer.Connected
                                          select peer.Ip).ToList();

                foreach (var peer in deadPeers)
                {
                    if (_peerSwarm.TryRemove(peer, out Peer deadPeer))
                    {
                        Log.Logger.Info($"Dead Peer {peer} removed from swarm.");
                    }
                }

                Log.Logger.Info("Queuing new peers for swarm ....");

                foreach (var peerDetails in peers)
                {
                    _peersTooSwarm.Add(peerDetails);
                }

                MainTracker.NumWanted = Math.Max(MainTracker.MaximumSwarmSize - _peerSwarm.Count, 0);

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
                if (MainTracker.Left != 0)
                {

                    Log.Logger.Info("Starting torrent download for MetaInfo data ...");

                    _dc.DownloadFinished.WaitOne();

                    MainTracker.ChangeStatus(Tracker.TrackerEvent.completed);

                    Log.Logger.Info("Whole Torrent finished downloading.");
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

                    MainTracker.StopAnnouncing();
                    if (_peerSwarm != null)
                    {
                        Log.Logger.Info("Closing peer sockets.");
                        foreach (var remotePeer in _peerSwarm.Values)
                        {
                            remotePeer.Close();
                        }
                    }
                    MainTracker.ChangeStatus(Tracker.TrackerEvent.stopped);

                    PeerNetwork.ShutdownListener(_listenerSocket);
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
        /// 
        /// </summary>
        /// <returns></returns>
        public List<PeerDetails> GetPeerSwarm()
        {
            return (from peer in _peerSwarm.Values
                    select new PeerDetails
                    {
                        ip = peer.Ip,
                        port = peer.Port
                    }).ToList();
        }
    }
}
