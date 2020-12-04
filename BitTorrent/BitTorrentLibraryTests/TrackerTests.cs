using System.Net.Sockets;
using System.Xml.Serialization;
//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent Tracker class.
//
// Copyright 2020.
//
using System;
using System.Text;
using Xunit;
using Moq;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class TrackerTests
    {
        // Basic mocking setup for a dummy tracker to run.Might be
        // expanded at a later date with more enhanced tests.
        private class MockAnnouncerFactory : IAnnouncerFactory
        {
            private readonly Mock<IAnnouncer> _mockAnnouncer;
            private AnnounceResponse _response;
            private AnnounceResponse AnnouceResponseRecord()
            {
                _response.announceCount++;
                return _response;
            }
            public MockAnnouncerFactory()
            {
                _mockAnnouncer = new Mock<IAnnouncer>();
                _mockAnnouncer.SetupAllProperties();
                _mockAnnouncer.Setup(a => a.Announce(It.IsAny<Tracker>())).Returns(AnnouceResponseRecord);
                _response = new AnnounceResponse(); ;
            }
            public IAnnouncer Create(string url)
            {
                return _mockAnnouncer.Object;
            }
        }
        [Fact]
        public void TestSucessfullyCreateTrackerAndStartAnnoucing()
        {
            MockAnnouncerFactory mockAnnoucerFactory = new MockAnnouncerFactory();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc, mockAnnoucerFactory);
            agent.AddTorrent(tc);
            agent.AttachPeerSwarmQueue(tracker);
            tracker.StartAnnouncing();
            TorrentDetails torrentDetails = agent.GetTorrentDetails(tc);
            Assert.Equal(TrackerStatus.Running, torrentDetails.trackerStatus);
        }
        [Fact]
        public void TestNullPassedAsTorrentContext()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Assert.Throws<ArgumentNullException>(() => { Tracker tracker = new Tracker(null); });
        }
        [Fact]
        public void TestDataReadFromTorrentFile()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
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
            MockAnnouncerFactory mockAnnoucerFactory = new MockAnnouncerFactory();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc, mockAnnoucerFactory);
            agent.AddTorrent(tc); ;
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tracker.StartAnnouncing());
            Assert.Equal("BitTorrent Error: Peer swarm queue has not been set.", error.Message);
        }
        [Fact]
        public void TestStopAnnouncingOnOneThatHasNotBeenStarted()
        {
            MockAnnouncerFactory mockAnnoucerFactory = new MockAnnouncerFactory();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc, mockAnnoucerFactory);
            agent.AddTorrent(tc);
            agent.AttachPeerSwarmQueue(tracker);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tracker.StopAnnouncing());
            Assert.Equal("BitTorrent Error: Tracker is not running so cannot be stopped.", error.Message);
        }
        [Fact]
        public void TestStartAnnoucingCalledOnAlreadyWhenAlreadyAnnoucing()
        {
            MockAnnouncerFactory mockAnnoucerFactory = new MockAnnouncerFactory();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc, mockAnnoucerFactory);
            agent.AddTorrent(tc);
            agent.AttachPeerSwarmQueue(tracker);
            tracker.StartAnnouncing();
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tracker.StartAnnouncing());
            Assert.Equal("BitTorrent Error: Tracker cannot be started as is already running.", error.Message);
        }
        [Fact]
        public void TestStopAnnoucingWhenTrackerAlreadyStopped()
        {
            MockAnnouncerFactory mockAnnoucerFactory = new MockAnnouncerFactory();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc, mockAnnoucerFactory);
            agent.AddTorrent(tc);
            agent.AttachPeerSwarmQueue(tracker);
            tracker.StartAnnouncing();
            tracker.StopAnnouncing();
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tracker.StopAnnouncing());
            Assert.Equal("BitTorrent Error: Tracker is not running so cannot be stopped.", error.Message);
        }
        [Fact]
        public void TestSetSeedingIntervalWhenNotSeeding()
        {
            MockAnnouncerFactory mockAnnoucerFactory = new MockAnnouncerFactory();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc, mockAnnoucerFactory);
            agent.AddTorrent(tc);
            agent.AttachPeerSwarmQueue(tracker);
            tracker.StartAnnouncing();
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tracker.SetSeedingInterval(30));
            Assert.Equal("BitTorrent Error: Cannot change interval as torrent is not seeding.", error.Message);
        }
        [Fact]
        public void TestSetSeedingIntervalWhenSeeding()
        {
            MockAnnouncerFactory mockAnnoucerFactory = new MockAnnouncerFactory();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp", true);
            Tracker tracker = new Tracker(tc, mockAnnoucerFactory);
            agent.AddTorrent(tc);
            agent.AttachPeerSwarmQueue(tracker);
            tracker.StartAnnouncing();
            agent.StartTorrent(tc);
            try
            {
                tracker.SetSeedingInterval(30);
            }
            catch (Exception ex)
            {
                Assert.True(false, "Should not throw execption here but it did. " + ex.Message);
            }
            Assert.True(true);
        }
    }
}