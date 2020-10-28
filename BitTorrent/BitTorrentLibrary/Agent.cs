//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: All the high level torrent processing including download/upload
// of torrent pieces and updating the peers in the current swarm. Any  peers that
// are connected then have a piece assembler task created for them that puts together
// pieces that they request from the torrent before being written to disk.
//
//TODO:Support for multiple running torrents
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
        void Start();
    }

    /// <summary>
    /// Agent class definition.
    /// </summary>
    public class Agent : IAgent
    {

        private bool _agentRunning = false;                             // == true while agent is up and running.
        private readonly DownloadContext _dc;                           // Torrent download context
        private readonly HashSet<string> _deadPeers;                    // Dead peers list
        private readonly Assembler _pieceAssembler;                     // Piece assembler for agent
        private Socket _listenerSocket;                                 // Connection listener socket
        public BlockingCollection<PeerDetails> PeerSwarmQueue { get; }  // Queue of peers to add to swarm

        /// <summary>
        /// Start assembly task for connection with remote peer. If for any reason
        /// the connection fails then the peers ip is put into an dead peer list (set)
        /// so that no further connections are attempted.
        /// </summary>
        /// <param name="remotePeer"></param>
        private void StartPieceAssemblyTask(Peer remotePeer)
        {

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
        private void PeerConnectCreatorTask()
        {

            while (!PeerSwarmQueue.IsCompleted && _agentRunning)
            {
                PeerDetails peer = PeerSwarmQueue.Take();
                try
                {
                    // Only add peers that are not already there and is maximum swarm size hasnt been reached
                    if (!_deadPeers.Contains(peer.ip) && !_dc.PeerSwarm.ContainsKey(peer.ip) && _dc.PeerSwarm.Count < _dc.MaximumSwarmSize)
                    {
                        StartPieceAssemblyTask(new Peer(peer.ip, peer.port, _dc));
                    }
                }
                catch (Exception)
                {
                    Log.Logger.Info($"Failed to connect to {peer.ip}.Added to dead per list.");
                    _deadPeers.Add(peer.ip);
                }
            }

        }
        /// <summary>
        /// Listen for remote peer connects and on success start peer task then add it to swarm.
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

                    // Only add peers that are not already there and is maximum swarm size hasnt been reached
                    if (!_dc.PeerSwarm.ContainsKey(endPoint.Item1) && _dc.PeerSwarm.Count < _dc.MaximumSwarmSize)
                    {
                        StartPieceAssemblyTask(new Peer(endPoint.Item1, endPoint.Item2, _dc, remotePeerSocket));
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
        public Agent(DownloadContext dc, Assembler pieceAssembler)
        {
            _dc = dc;
            _pieceAssembler = pieceAssembler;
            PeerSwarmQueue = new BlockingCollection<PeerDetails>();
            _deadPeers = new HashSet<string>();
            Task.Run(() => PeerListenCreatorTask());
            Task.Run(() => PeerConnectCreatorTask());
            _agentRunning = true;
            _deadPeers.Add("192.168.1.1"); // WITHOUT THIS HANGS (FOR ME)

        }
        ~Agent()
        {
            PeerSwarmQueue.CompleteAdding();
        }
        /// <summary>
        /// Download a torrent using an piece assembler per connected peer.
        /// </summary>
        public void Download()
        {
            try
            {
                if (_dc.MainTracker.Left != 0)
                {
                    Log.Logger.Info("Starting torrent download for MetaInfo data ...");
                    _dc.Status = TorrentStatus.Downloading;
                    _dc.DownloadFinished.WaitOne();
                    _dc.MainTracker.ChangeStatus(Tracker.TrackerEvent.completed);
                    Log.Logger.Info("Whole Torrent finished downloading.");
                }

                _dc.Status = TorrentStatus.Seeding;

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

                    _dc.MainTracker.StopAnnouncing();
                    if (_dc.PeerSwarm != null)
                    {
                        Log.Logger.Info("Closing peer sockets.");
                        foreach (var remotePeer in _dc.PeerSwarm.Values)
                        {
                            remotePeer.Close();
                        }
                    }
                    _dc.MainTracker.ChangeStatus(Tracker.TrackerEvent.stopped);
                    PeerNetwork.ShutdownListener();
                    _dc.Status = TorrentStatus.Stopped;
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
                _dc.Status = TorrentStatus.Started;
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
                _dc.Status = TorrentStatus.Paused;
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
                status = _dc.Status,

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
