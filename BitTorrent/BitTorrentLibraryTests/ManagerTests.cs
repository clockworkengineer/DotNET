//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent Manager class.
//
// Copyright 2020.
//
using System;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class ManagerTests
    {
        [Fact]
        public void TestCreateManager()
        {
            try
            {
                Manager manager = new Manager();
            }
            catch (Exception ex)
            {
                Assert.True(false, "Should not throw execption here but it did. " + ex.Message);
            }
            Assert.True(true);
        }
        [Fact]
        public void TestPassNullForInfoHashToGetTorrentContext()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentNullException>(() => { manager.GetTorrentContext(null, out TorrentContext _); });
        }
        [Fact]
        public void TestPassNullForTorrentContextToAddTorrentContext()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentNullException>(() => { manager.GetTorrentContext(null, out TorrentContext _); });
        }
        [Fact]
        public void TestPassNullForTorrentContextToRemoveTorrentContext()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentNullException>(() => { manager.RemoveTorrentContext(null); });
        }
        [Fact]
        public void TestPassNullInfoHashToGetPeer()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentNullException>(() => { manager.GetPeer(null, "127.0.0.1", out Peer Peer); });
        }
        [Fact]
        public void TestPassNullIpToGetPeer()
        {
            byte[] infoHash = new byte[20];
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.GetPeer(infoHash, null, out Peer Peer); });
        }
        [Fact]
        public void TestPassEmptyStringIpToGetPeer()
        {
            byte[] infoHash = new byte[20];
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.GetPeer(infoHash, "", out Peer Peer); });
        }
        [Fact]
        public void TestPassNullIpToAddToDeadPeerList()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.AddToDeadPeerList(null); });
        }
        [Fact]
        public void TestPassEmptyStringIpToAddToDeadPeerList()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.AddToDeadPeerList(""); });
        }
        [Fact]
        public void TestPassNullIpToRemoveFromDeadPeerList()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.RemoveFromDeadPeerList(null); });
        }
        [Fact]
        public void TestPassEmptyStringIpRemoveFromDeadPeerList()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.RemoveFromDeadPeerList(""); });
        }
        [Fact]
        public void TestPassNullIpToIsPeerDead()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.IsPeerDead(null); });
        }
        [Fact]
        public void TestPassEmptyStringIpPeerIsDead()
        {
            Manager manager = new Manager();
            Assert.Throws<ArgumentException>(() => { manager.IsPeerDead(""); });
        }
    }
}