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
        public void TestCreateAgentWithNullManager()
        {
            Assert.Throws<ArgumentNullException>(() => { Agent agent = new Agent(null, new Assembler()); });
        }
        [Fact]
        public void TestCreateAgentWithNullAssembler()
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
        public void TestNullForAddTorrentToAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.AddTorrent(null));
        }
        [Fact]
        public void TestNullForRemoveTorrentFromAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.RemoveTorrent(null));
        }
        [Fact]
        public void TestNullForCloseTorrent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());
            agent.Startup();
            Assert.Throws<ArgumentNullException>(() => agent.CloseTorrent(null));
        }
        [Fact]
        public void TestAddAlreadyAddedTorrentContextToAgent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            agent.AddTorrent(tc);
            Error error = Assert.Throws<Error>(() => agent.AddTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to add torrent context.", error.Message);
        }
        [Fact]
        public void TestRemoveNonExistantTorrentContextFromAgent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Error error = Assert.Throws<Error>(() => agent.RemoveTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failed to remove torrent context.", error.Message);
        }
                [Fact]
        public void TestNullStartTorrentInAgent()
        {
            Agent agent = new Agent(new Manager(), new Assembler());;
            Assert.Throws<ArgumentNullException>(() => agent.StartTorrent(null));
        }
               [Fact]
        public void TestStartTorrentNotAddedToAgent()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            Agent agent = new Agent(new Manager(), new Assembler());
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Error error = Assert.Throws<Error>(() => agent.StartTorrent(tc));
            Assert.Equal("BitTorrent (Agent) Error : Failure to start torrent context.Torrent hasnt been added to agent.", error.Message);
        }
    }
}