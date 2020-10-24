using System;
using System.Collections.Generic;

namespace BitTorrentLibrary
{
    public struct TorrentDetails
    {
        public List<PeerDetails> peers;
        public UInt64 uploadedBytes;
        public UInt64 downloadedBytes;
        public byte[] InfoHash;
    }
}
