using System.Collections.Generic;

namespace BitTorrent
{
    public struct PeerDetails
    {
        public string _peerID;
        public string ip;
        public int port;
    }

    public struct AnnounceResponse
    {
        public int announceCount;
        public int statusCode;
        public string statusMessage;
        public int interval;
        public int minInterval;
        public string trackerID;
        public int complete;
        public int incomplete;
        public List<PeerDetails> peers;
    };
}
