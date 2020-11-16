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
            Error error = Assert.Throws<Error>(() => { agent.Startup(); });
            Assert.Equal("BitTorrent (Agent) Error : Failure to startup agent.Agent is already running.", error.Message);
        }
        [Fact]
        public void TestShutdownAlreadyShutdownAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            agent.ShutDown();
            Error error = Assert.Throws<Error>(() => { agent.ShutDown(); });
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
            Error error = Assert.Throws<Error>(() => agent.AddTorrent(tc));
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
            Error error = Assert.Throws<Error>(() => agent.RemoveTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to remove torrent context.", error.Message);
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
            Error error = Assert.Throws<Error>(() => agent.StartTorrent(tc));
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
            Error error = Assert.Throws<Error>(() => agent.CloseTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failure to close torrent context.Torrent hasnt been added to agent.", error.Message);
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
            Error error = Assert.Throws<Error>(() => agent.PauseTorrent(tc));
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
            Error error = Assert.Throws<Error>(() => agent.PauseTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failure to pause torrent context.The torrent is currentlu not in a running state.", error.Message);
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
            Error error = Assert.Throws<Error> (() => agent.AddTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to add torrent context.Torrent does not have a tracker associated with it.", error.Message);

        }

    }
}