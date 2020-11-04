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

using System.Threading.Tasks;

namespace BitTorrentLibrary
{
    public interface IAgent
    {
        void Add(TorrentContext tc);
        void AttachPeerSwarmQueue(Tracker tracker);
        void Close(TorrentContext tc);
        void DetachPeerSwarmQueu(Tracker tracker);
        void Download(TorrentContext tc);
        Task DownloadAsync(TorrentContext tc);
        TorrentDetails GetTorrentDetails(TorrentContext tc);
        void Pause(TorrentContext tc);
        void Remove(TorrentContext tc);
        void ShutDown();
        void Start(TorrentContext tc);
        void Startup();
    }
}
