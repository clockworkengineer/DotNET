
using System;
using System.Text;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class TrackerTests
    {
        [Fact]
        public void TestNullPassedAsTorrentContext()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Assert.Throws<ArgumentNullException>(() => { Tracker tracker = new Tracker(null); });
        }
        [Fact]
        public void TestDataReadFromTorrentFile()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            Assert.Equal(1, tracker.Compact);
            Assert.Equal(TrackerEvent.None, tracker.Event);
            Assert.Equal(2000, tracker.Interval);
            Assert.Equal(Host.GetIP(), tracker.Ip);
            Assert.Equal(351874, (int)tracker.Left);
            Assert.Equal(5, tracker.NumWanted);
            Assert.Equal("-AZ1000-BMt9tgTUwEiH", tracker.PeerID);
            Assert.Equal(6881, tracker.Port);
            Assert.Equal("http://192.168.1.215:9005/announce", tracker.TrackerURL);
            Assert.Equal(0, (int)tracker.Uploaded);
            byte[] infoHash = tracker.InfoHash;
            StringBuilder actual = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
            {
                actual.AppendFormat("{0:x2}", b);
            }
            Assert.Equal("7fd1a2631b385a4cc68bf15040fa375c8e68cb7e", actual.ToString());

        }
        [Fact]
        public void TestStartAnnouncingWhenNoPeerSwarmQueueHasBeenAttached()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tracker.StartAnnouncing());
            Assert.Equal("BitTorrent (Tracker) Error: Peer swarm queue has not been set.", error.Message);
        }
        [Fact]
        public void TestStartAnnouncingWhenPeerSwarmQueueHasBeenAttached()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            agent.AttachPeerSwarmQueue(tracker);
            Assert.Throws<BitTorrentException>(() => tracker.StartAnnouncing());
            Assert.Equal(TrackerStatus.Stalled, tracker.trackerStatus);
        }
        [Fact]
        public void TestStopAnnouncingOnOneThatHasNotBeenStarted()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            agent.AttachPeerSwarmQueue(tracker);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tracker.StopAnnouncing());
            Assert.Equal("BitTorrent (Tracker) Error: Tracker is not running so cannot be stopped.", error.Message);
        }
    }
}