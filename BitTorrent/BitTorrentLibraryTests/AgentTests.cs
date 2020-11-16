using System;
using System.Text;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class AgentTests
    {
        [Fact]
        public void TestCreateAgentObjectNotNull()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            Assert.NotNull(agent);
        }
        [Fact]
        public void TestNullManagerPassedToConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => { Agent agent = new Agent(null, new Assembler()); });
        }
        [Fact]
        public void TestNullAssemblerPassedToConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => { Agent agent = new Agent(new Manager(), null); });
        }
        [Fact]
        public void TestStartupAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            Assert.True(agent.Running);
        }
        [Fact]
        public void TestStartupThenShutDownAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            agent.ShutDown();
            Assert.False(agent.Running);
        }
        [Fact]
        public void TestStartupAlreadyStartedAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => { agent.Startup(); });
            Assert.Equal("BitTorrent (Agent) Error : Failure to startup agent.Agent is already running.", error.Message);
        }
        [Fact]
        public void TestShutdownAlreadyShutdownAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            agent.ShutDown();
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => { agent.ShutDown(); });
            Assert.Equal("BitTorrent (Agent) Error : Failed to shutdown agent.Agent already shutdown.", error.Message);
        }
        [Fact]
        public void TestNullPassedToAddTorrent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.AddTorrent(null));
        }
        [Fact]
        public void TestNullPassedToRemoveTorrent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.RemoveTorrent(null));
        }
        [Fact]
        public void TestNullPassedToCloseTorrent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.CloseTorrent(null));
        }
        [Fact]
        public void TestAddTorrentThatIsAlreadyAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            agent.AddTorrent(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.AddTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to add torrent context.Torrent most probably has already been added.", error.Message);
        }
        [Fact]
        public void TestRemoveTorrentThatHasntBeenAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.RemoveTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to remove torrent context.It probably has been removed alrady or never added.", error.Message);
        }
        [Fact]
        public void TestNullTorrentContextPassedStartTorrent()
        {
            Agent agent = new Agent(new Manager(), new Assembler()); ;
            Assert.Throws<ArgumentNullException>(() => agent.StartTorrent(null));
        }
        [Fact]
        public void TestStartTorrentNotAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.StartTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failure to start torrent context.Torrent hasnt been added to agent.", error.Message);
        }
        [Fact]
        public void TestCloseTorrentNotAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.CloseTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failure to close torrent context.Torrent hasnt been added to agent or may already have been closed.", error.Message);
        }
        [Fact]
        public void TestNullPassedToPauseTorrent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            Assert.Throws<ArgumentNullException>(() => agent.PauseTorrent(null));
        }
        [Fact]
        public void TestPauseTorrentNotAdded()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.PauseTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failure to pause torrent context.Torrent hasnt been added to agent.", error.Message);
        }
        [Fact]
        public void TestPauseTorrentNotStarted()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            agent.AddTorrent(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.PauseTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failure to pause torrent context.The torrent is currently not in a running state.", error.Message);
        }
        [Fact]
        public void TestStartAddedTorrent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
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
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.AddTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to add torrent context.Torrent does not have a tracker associated with it.", error.Message);
        }
        [Fact]
        public void TestNullPassedToGetTorrentDetails()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
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
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            agent.AddTorrent(tc);
            TorrentDetails details = agent.GetTorrentDetails(tc);
            Assert.Equal(0, details.deadPeers);
            Assert.Equal(0, (int)details.downloadedBytes);
            Assert.Equal("singlefile.torrent", details.fileName);
            Assert.Equal(file.MetaInfoDict["info hash"], details.infoHash);
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
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => { TorrentDetails details = agent.GetTorrentDetails(tc); });
            Assert.Equal("BitTorrent (Agent) Error : Failure to get torrent details.Torrent hasnt been added to agent.", error.Message);
        }
        [Fact]
        public void TestRemoveTorrentThatHasNotBeenAddedToAgent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Tracker tracker = new Tracker(tc);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => agent.RemoveTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to remove torrent context.It probably has been removed alrady or never added.", error.Message);
        }
    }
}