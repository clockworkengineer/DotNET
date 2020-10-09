//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: All the high level torrent processing including download/upload
// of files and updating the peers in the current swarm downloading. 
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
using System.Net;
using System.Net.Sockets;

namespace BitTorrentLibrary
{
    public delegate void ProgessCallBack(Object progressData);      // Download progress callback

    /// <summary>
    /// File Agent class definition.
    /// </summary>
    public class Agent
    {
        private readonly BlockingCollection<PeerDetails> _peersTooSwarm;   // Peers to add to swarm queue
        private readonly Task _downloaderListenerTask;                     // Peer swarm listener task
        private readonly Task _uploaderListenerTask;                       // Upload peer connection listener task
        private readonly HashSet<string> _deadPeersList;                   // Peers that failed to connect
        private readonly ManualResetEvent _downloadFinished;               // WaitOn when download finished == true
        private readonly Downloader _torrentDownloader;                    // Downloader for torrent
        private readonly Assembler _pieceAssembler;                        // Piece assembler for agent
        private readonly Disassembler _pieceDisassembler;                  // Piece disassembler for agent
        private ConcurrentDictionary<string, Peer> _peerSwarm;             // Connected remote peers in swarm
        private ConcurrentDictionary<string, Peer> _peerUploaders;         // Connected remote uploading peers 
        public byte[] InfoHash { get; }                                    // Torrent info hash
        public string TrackerURL { get; }                                  // Main Tracker URL
        public Tracker MainTracker { get; set; }                           // Main torrent tracker
        public UInt64 Left => _torrentDownloader.Dc.BytesLeftToDownload(); // Number of bytes left to download;

        /// <summary>
        /// Inspects download peer queue and connects and creates piece assembler task before
        /// adding to swarm.
        /// </summary>
        private void DownloaderListenerTask()
        {

            while (!_peersTooSwarm.IsCompleted)
            {
                PeerDetails peer = _peersTooSwarm.Take();
                try
                {
                    if (!_peerSwarm.ContainsKey(peer.ip) && !_deadPeersList.Contains(peer.ip))
                    {
                        Peer remotePeer = new Peer(peer.ip, peer.port, InfoHash, _torrentDownloader.Dc);
                        remotePeer.Connect();
                        if (remotePeer.Connected)
                        {
                            if (_peerSwarm.TryAdd(remotePeer.Ip, remotePeer))
                            {
                                Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                                remotePeer.AssemblerTask = Task.Run(() => _pieceAssembler.AssemblePieces(remotePeer, _downloadFinished));
                            } else {
                                _deadPeersList.Add(peer.ip);
                                remotePeer.Close();
                            }
                        }
                        else
                        {
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

        /// <summary>
        /// Listen for remote peer connects and on success start peer task.
        /// </summary>
        private void UploaderListenerTask()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Host.GetIP());
            IPEndPoint localEndPoint = new IPEndPoint(ipHostInfo.AddressList[0], (int)Host.DefaultPort);
            Socket listener = new Socket(ipHostInfo.AddressList[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Socket remotePeerSocket;

            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                Log.Logger.Info("Waiting for remote peer connect...");

                remotePeerSocket = listener.Accept();

                Log.Logger.Info("Remote peer connected...");

                string remotePeerIP = ((IPEndPoint)(remotePeerSocket.RemoteEndPoint)).Address.ToString();
                UInt32 remotePeerPort = (UInt32)((IPEndPoint)(remotePeerSocket.RemoteEndPoint)).Port;

                Log.Logger.Info($"++Remote peer IP = {remotePeerIP}:{remotePeerPort}.");

                if (remotePeerIP == "192.168.1.1")
                { // NO IDEA WHATS BEHIND THIS AT PRESENT (HANGS IF WE DONT CLOSE THIS)
                    remotePeerSocket.Close();
                    continue;
                }

                Peer remotePeer = new Peer(remotePeerSocket, remotePeerIP, remotePeerPort, InfoHash, _torrentDownloader.Dc);
                remotePeer.Accept();
                if (remotePeer.Connected)
                {
                    _peerUploaders.TryAdd(remotePeer.Ip, remotePeer);
                    Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] from remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                    remotePeer.UploaderTask = Task.Run(() => _pieceDisassembler.DisassemlePieces(remotePeer));
                }
                else
                {
                    _deadPeersList.Add(remotePeer.Ip);
                }
            }

        }

        /// <summary>
        /// Initializes a new instance of a torrent agent.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public Agent(MetaInfoFile torrentFile, Downloader downloader, Assembler pieceAssembler = null, Disassembler pieceDisassembler = null)
        {
            _torrentDownloader = downloader;
            _torrentDownloader.BuildDownloadedPiecesMap();
            _pieceAssembler = pieceAssembler;
            _pieceDisassembler = pieceDisassembler;
            _peerSwarm = new ConcurrentDictionary<string, Peer>();
            _peerUploaders = new ConcurrentDictionary<string, Peer>();
            _peersTooSwarm = new BlockingCollection<PeerDetails>();
            InfoHash = torrentFile.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentFile.MetaInfoDict["announce"]);
            _deadPeersList = new HashSet<string>();
            _downloadFinished = new ManualResetEvent(false);
            if (pieceDisassembler != null)
            {
                _uploaderListenerTask = Task.Run(() => UploaderListenerTask());
            }
            if (pieceAssembler != null)
            {
                _downloaderListenerTask = Task.Run(() => DownloaderListenerTask());
            }


        }
        ~Agent()
        {
            _peersTooSwarm.CompleteAdding();
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

                List<string> deadPeers = (from peer in _peerSwarm.Values
                                          where !peer.Connected
                                          select peer.Ip).ToList();

                foreach (var ip in deadPeers)
                {
                    Peer deadPeer;
                    if (_peerSwarm.TryRemove(ip, out deadPeer))
                    {
                        _deadPeersList.Add(ip);
                        Log.Logger.Info($"Dead Peer {ip} removed from swarm.");
                    }
                }

                Log.Logger.Info("Queuing new peers for swarm ....");

                foreach (var peer in peers)
                {
                    _peersTooSwarm.Add(peer);
                }

                MainTracker.NumWanted = Math.Max(MainTracker.MaximumSwarmSize - _peerSwarm.Count, 0);

                Log.Logger.Info($"Number of peers in swarm  {_peerSwarm.Count}/{MainTracker.MaximumSwarmSize}. Active {_pieceAssembler.ActiveAssemblerTasks}.");

            }
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

                    _downloadFinished.WaitOne();

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
                foreach (var remotePeer in _peerSwarm.Values)
                {
                    remotePeer.Paused.Set();
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
        /// Pause downloading torrent.
        /// </summary>
        public void Pause()
        {
            try
            {
                foreach (var remotePeer in _peerSwarm.Values)
                {
                    remotePeer.Paused.Reset();
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
    }
}
