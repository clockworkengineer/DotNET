using System;
namespace BitTorrent
{
    public class Agent
    {
        private string _torrentFileName;
        private MetaInfoFile _torrentMetaInfo;
        private string _downloadPath;
        private Tracker _mainTracker;

        public string TorrentFileName { get => _torrentFileName; set => _torrentFileName = value; }
        public MetaInfoFile TorrentMetaInfo { get => _torrentMetaInfo; set => _torrentMetaInfo = value; }
        public string DownloadPath { get => _downloadPath; set => _downloadPath = value; }
        public Tracker MainTracker { get => _mainTracker; set => _mainTracker = value; }

        public Agent(string torrentFileName, String downloadPath)
        {
            TorrentFileName = torrentFileName;
            DownloadPath = downloadPath;
        }

        public void load()
        {
            TorrentMetaInfo = new MetaInfoFile(TorrentFileName);
            TorrentMetaInfo.load();
            TorrentMetaInfo.parse();

            MainTracker = new Tracker(TorrentMetaInfo, PeerID.get());

        }
    }
}
