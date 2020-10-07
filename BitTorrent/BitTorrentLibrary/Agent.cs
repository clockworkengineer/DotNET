//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: All the high level torrent processing including download/upload
// of files and updating the peers in the current swarm downloading. 
// 
// NOTE: ONLY DOWNLOAD SUPPORTED AT PRESENT (ITS ONE BIG LEECHER).
//
// Copyright 2019.
//

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace BitTorrentLibrary
{
    public delegate void ProgessCallBack(Object progressData);      // Download progress 

    /// <summary>
    /// File Agent class definition.
    /// </summary>
    public class Agent
    {
        private Task _uploaderListenerTask = null;                   // Upload peer connection listener task
        private readonly HashSet<string> _deadPeersList;             // Peers that failed to connect
        private readonly ManualResetEvent _downloadFinished;         // WaitOn when download finsihed == true
        private readonly Downloader _torrentDownloader;              // Downloader for torrent
        private readonly Assembler _pieceAssembler;                  // Piece assembler for agent
        public Dictionary<string, Peer> RemotePeerSwarm { get; set; }// Connected remote peers in swarm
        public Dictionary<string, Peer> RemotePeerUpload { get; set; }// Connected remote uploading peers 
        public byte[] InfoHash { get; }                              // Torrent info hash
        public string TrackerURL { get; }                            // Main Tracker URL
        public Tracker MainTracker { get; set; }                     // Main torrent tracker
        public UInt64 Left => _torrentDownloader.Dc.TotalBytesToDownload - _torrentDownloader.Dc.TotalBytesDownloaded; //Number of bytes left to download;

        /// <summary>
        /// 
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
                    RemotePeerUpload.Add(remotePeer.Ip, remotePeer);
                    Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] from remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                    remotePeer.UploaderTask = Task.Run(() => UploadPieces(remotePeer));
                }
                else
                {
                    _deadPeersList.Add(remotePeer.Ip);
                }
            }

        }

        /// <summary>
        /// Upload requested pieces task
        /// </summary>
        /// <returns>Task reference on completion.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        private void UploadPieces(Peer remotePeer)
        {


            try
            {

            }
            catch (Exception ex)
            {

            }

        }

        /// <summary>
        /// Initializes a new instance of a torrent agent.
        /// </summary>
        /// <param name="torrentFileName">Torrent file name.</param>
        /// <param name="downloadPath">Download path.</param>
        public Agent(MetaInfoFile torrentFile, Downloader downloader, Assembler pieceAssembler, bool uploader = false)
        {
            _torrentDownloader = downloader;
            _torrentDownloader.BuildDownloadedPiecesMap();
            _pieceAssembler = pieceAssembler;
            RemotePeerSwarm = new Dictionary<string, Peer>();
            RemotePeerUpload = new Dictionary<string, Peer>();
            InfoHash = torrentFile.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentFile.MetaInfoDict["announce"]);
            _deadPeersList = new HashSet<string>();
            _downloadFinished = new ManualResetEvent(false);
            if (uploader)
            {
                _uploaderListenerTask = Task.Run(() => UploaderListenerTask());
            }

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

                List<string> deadPeers = (from peer in RemotePeerSwarm.Values
                                          where !peer.Connected
                                          select peer.Ip).ToList();

                foreach (var ip in deadPeers)
                {
                    RemotePeerSwarm.Remove(ip);
                    _deadPeersList.Add(ip);
                    Log.Logger.Info($"Dead Peer {ip} removed from swarm.");
                }

                Log.Logger.Info("Connecting any new peers to swarm ....");

                foreach (var peer in peers)
                {
                    try
                    {
                        if (!RemotePeerSwarm.ContainsKey(peer.ip) && !_deadPeersList.Contains(peer.ip))
                        {
                            Peer remotePeer = new Peer(peer.ip, peer.port, InfoHash, _torrentDownloader.Dc);
                            remotePeer.Connect();
                            if (remotePeer.Connected)
                            {
                                RemotePeerSwarm.Add(remotePeer.Ip, remotePeer);
                                Log.Logger.Info($"BTP: Local Peer [{ PeerID.Get()}] to remote peer [{Encoding.ASCII.GetString(remotePeer.RemotePeerID)}].");
                                remotePeer.AssemblerTask = Task.Run(() => _pieceAssembler.AssemblePieces(remotePeer, _downloadFinished));
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

            MainTracker.NumWanted = Math.Max(MainTracker.MaximumSwarmSize - RemotePeerSwarm.Count, 0);

            Log.Logger.Info($"Number of peers in swarm  {RemotePeerSwarm.Count}/{MainTracker.MaximumSwarmSize}. Active {_pieceAssembler.ActiveAssemblerTasks}.");

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
                    Start();

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
                if (RemotePeerSwarm != null)
                {
                    Log.Logger.Info("Closing peer sockets.");
                    foreach (var remotePeer in RemotePeerSwarm.Values)
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
                foreach (var remotePeer in RemotePeerSwarm.Values)
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
                foreach (var remotePeer in RemotePeerSwarm.Values)
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
