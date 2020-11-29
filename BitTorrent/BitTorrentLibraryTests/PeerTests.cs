//
// Author: Robert Tizzard
//
// Description: XUnit tests for BiTorrent Peer class.
//
// Copyright 2020.
//
using System;
using System.Net.Sockets;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class PeerTests
    {
        [Fact]
        public void TestEmptyStringPassedForIPInPeerConstruction()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => { Peer peer = new Peer("", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)); });
            Assert.Equal("'ip' cannot be null or empty (Parameter 'ip')", error.Message);
        }
        [Fact]
        public void TestNullPassedForIPInPeerConstruction()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => { Peer peer = new Peer(null, 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)); });
            Assert.Equal("'ip' cannot be null or empty (Parameter 'ip')", error.Message);
        }
        [Fact]
        public void TestNullPassedForSocketInPeerConstruction()
        {
            ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => { Peer peer = new Peer("127.0.0.1", 6881, null); });
            Assert.Equal("Value cannot be null. (Parameter 'socket')", error.Message);
        }
        [Fact]
        public void TestNullPassedForTorrentContextInPeerConstruction()
        {
            ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => { Peer peer = new Peer("127.0.0.1", 6881, null, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)); });
            Assert.Equal("Value cannot be null. (Parameter 'tc')", error.Message);
        }
        [Fact]
        public void TestNullPassedToSetTorrentContext()
        {
            Peer peer = new Peer("127.0.0.1", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Assert.Throws<ArgumentNullException>(() => peer.SetTorrentContext(null));
        }
        [Fact]
        public void TestNullPassedToWrite()
        {
            Peer peer = new Peer("127.0.0.1", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Assert.Throws<ArgumentNullException>(() => peer.Write(null));
        }
        [Fact]
        public void TestNullPassedToRead()
        {
            Peer peer = new Peer("127.0.0.1", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Assert.Throws<ArgumentNullException>(() => peer.Read(null));
        }
        [Fact]
        public void TestNullPassedToHandshake()
        {
            Peer peer = new Peer("127.0.0.1", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Assert.Throws<ArgumentNullException>(() => peer.Handshake(null));
        }
        [Fact]
        public void TestCloseCalledTwiceOnUnconnectedPeer()
        {
            try
            {
                Peer peer = new Peer("127.0.0.1", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
                peer.Close();
                peer.Close();
            }
            catch (Exception ex)
            {
                Assert.True(false, "Should not throw execption here but it did. " + ex.Message);
            }
            Assert.True(true);
        }
        [Fact]
        public void TestCallIsPieceOnRemotePeerWhenNoTorrentContextHasBeenSet()
        {
            Peer peer = new Peer("127.0.0.1", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Exception error = Assert.Throws<Exception>(() => peer.IsPieceOnRemotePeer(0));
            Assert.Equal("Torrent context needs to be set for peer.", error.Message);
        }
        [Fact]
        public void TestCallSetPieceOnRemotePeerWhenNoTorrentContextHasBeenSet()
        {
            Peer peer = new Peer("127.0.0.1", 6881, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Exception error = Assert.Throws<Exception>(() => peer.SetPieceOnRemotePeer(0));
            Assert.Equal("Torrent context needs to be set for peer.", error.Message);
        }
        [Fact]
        public void TestSucessfullyCreatePeer()
        {
            try
            {
                MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
                file.Parse();
                Manager manager = new Manager();
                TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
                Peer peer = new Peer("127.0.0.1", 6881, tc, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            }
            catch (Exception ex)
            {
                Assert.True(false, "Should not throw execption here but it did. " + ex.Message);
            }
            Assert.True(true);
        }
        [Fact]
        public void TestCheckPropertiesSucessfullyCreatePeer()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Peer peer = new Peer("127.0.0.1", 6881, tc, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Assert.Equal("127.0.0.1", peer.Ip);
            Assert.Equal(6881, peer.Port);
            Assert.Equal(22, peer.NumberOfMissingPieces);
        }
        [Fact]
        public void SetPieceOnRemotePeerWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Peer peer = new Peer("127.0.0.1", 6881, tc, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Assert.Throws<IndexOutOfRangeException>(() => peer.SetPieceOnRemotePeer(1000));
        }
    }
}