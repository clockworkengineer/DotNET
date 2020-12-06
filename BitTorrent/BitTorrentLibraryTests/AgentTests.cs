//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent Agent class.
//
// Copyright 2020.
//
using System;
using Xunit;
using Moq;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class AgentTests
    {
        [Fact]
        public void TestCreateAgentObjectNotNull()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            Assert.NotNull(agent);
        }
        [Fact]
        public void TestNullManagerPassedToConstructor()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Assert.Throws<ArgumentNullException>(() => { Agent agent = new Agent(null, new Assembler(), networkMock.Object); });
        }
        [Fact]
        public void TestNullAssemblerPassedToConstructor()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Assert.Throws<ArgumentNullException>(() => { Agent agent = new Agent(new Manager(), null, networkMock.Object); });
        }
        [Fact]
        public void TestStartupAgent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            Assert.True(agent.Running);
        }
        [Fact]
        public void TestStartupThenShutDownAgent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            agent.Shutdown();
            Assert.False(agent.Running);
        }
        [Fact]
        public void TestStartupAlreadyStartedAgent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => { agent.Startup(); }); Assert.Equal("BitTorrent Error: Failure to startup agent.Agent is already running.", error.Message);
        }
        [Fact]
        public void TestShutdownAlreadyShutdownAgent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            agent.Shutdown();
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => { agent.Shutdown(); });
            Assert.Equal("BitTorrent Error: Failed to shutdown agent.Agent already shutdown.", error.Message);
        }
        [Fact]
        public void TestNullPassedToAddTorrent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.AddTorrent(null));
        }
        [Fact]
        public void TestNullPassedToRemoveTorrent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.RemoveTorrent(null));
        }
        [Fact]
        public void TestNullPassedToCloseTorrent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.CloseTorrent(null));
        }
        [Fact]
        public void TestAddTorrentThatIsAlreadyAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            agent.AddTorrent(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.AddTorrent(tc));
            Assert.Equal("BitTorrent Error: Failed to add torrent context.Torrent most probably has already been added.", error.Message);
        }
        [Fact]
        public void TestRemoveTorrentThatHasntBeenAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.RemoveTorrent(tc));
            Assert.Equal("BitTorrent Error: Failed to remove torrent context.It probably has been removed alrady or never added.", error.Message);
        }
        [Fact]
        public void TestNullTorrentContextPassedStartTorrent()
        {
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            Assert.Throws<ArgumentNullException>(() => agent.StartTorrent(null));
        }
        [Fact]
        public void TestStartTorrentNotAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.StartTorrent(tc));
            Assert.Equal("BitTorrent Error: Failure to start torrent context.Torrent hasnt been added to agent.", error.Message);
        }
        [Fact]
        public void TestCloseTorrentNotAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.CloseTorrent(tc));
            Assert.Equal("BitTorrent Error: Failure to close torrent context.Torrent hasnt been added to agent or may already have been closed.", error.Message);
        }
        [Fact]
        public void TestNullPassedToPauseTorrent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            Assert.Throws<ArgumentNullException>(() => agent.PauseTorrent(null));
        }
        [Fact]
        public void TestPauseTorrentNotAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.PauseTorrent(tc));
            Assert.Equal("BitTorrent Error: Failure to pause torrent context.Torrent hasnt been added to agent.", error.Message);
        }
        [Fact]
        public void TestPauseTorrentNotStarted()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            agent.AddTorrent(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.PauseTorrent(tc));
            Assert.Equal("BitTorrent Error: Failure to pause torrent context.The torrent is currently not in a running state.", error.Message);
        }
        [Fact]
        public void TestStartAddedTorrentWhenAgentHasntBeenStarted()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            _ = new Tracker(tc);
            agent.AddTorrent(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.StartTorrent(tc));
            Assert.Equal("BitTorrent Error: Failure to start torrent context.Agent has not been started.", error.Message);
        }
        [Fact]
        public void TestStartAddedTorrent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            _ = new Tracker(tc);
            agent.AddTorrent(tc);
            agent.StartTorrent(tc);
            Assert.True(tc.Status == TorrentStatus.Downloading || tc.Status == TorrentStatus.Seeding);
        }
        [Fact]
        public void TestAddTorrentThatNotDoesHaveTracker()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.AddTorrent(tc));
            Assert.Equal("BitTorrent Error: Failed to add torrent context.Torrent does not have a tracker associated with it.", error.Message);
        }
        [Fact]
        public void TestNullPassedToGetTorrentDetails()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            agent.AddTorrent(tc);
            agent.StartTorrent(tc);
            Assert.Throws<ArgumentNullException>(() => agent.GetTorrentDetails(null));
        }
        [Fact]
        public void TestGetTorrentDetailsReturnsCorrectDetails()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            _ = new Tracker(tc);
            agent.AddTorrent(tc);
            TorrentDetails details = agent.GetTorrentDetails(tc);
            Assert.Equal(0, details.deadPeers);
            Assert.Equal(0, (int)details.downloadedBytes);
            Assert.Equal(Constants.SingleFileTorrent, details.fileName);
            Assert.Equal(file.GetInfoHash(), details.infoHash);
            Assert.Equal(22, details.missingPiecesCount);
            Assert.Equal(0, (int)details.peers.Count);
            Assert.Equal(TorrentStatus.Initialised, details.status);
            Assert.Equal(0, details.swarmSize);
            Assert.Equal(TrackerStatus.Stopped, details.trackerStatus);
            Assert.Null(details.trackerStatusMessage);
            Assert.Equal(0, (int)details.uploadedBytes);
        }
        [Fact]
        public void TestGetTorrentDetailsOfTorrentNotAddedToAgent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => { TorrentDetails details = agent.GetTorrentDetails(tc); });
            Assert.Equal("BitTorrent Error: Failure to get torrent details.Torrent hasnt been added to agent.", error.Message);
        }
        [Fact]
        public void TestRemoveTorrentThatHasNotBeenAddedToAgent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Mock<IAgentNetwork> networkMock = new Mock<IAgentNetwork>();
            Agent agent = new Agent(new Manager(), new Assembler(), networkMock.Object);
            agent.Startup();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), Constants.DestinationDirectory);
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.RemoveTorrent(tc));
            Assert.Equal("BitTorrent Error: Failed to remove torrent context.It probably has been removed alrady or never added.", error.Message);
        }
    }
}