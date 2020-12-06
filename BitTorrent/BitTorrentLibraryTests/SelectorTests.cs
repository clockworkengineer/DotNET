//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent Torrent Context class.
//
// Copyright 2020.
//
using System;
using System.Net.Sockets;
using System.Collections.Generic;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class SelectorTests
    {
        [Fact]
        public void TestCreateSelector()
        {
            try
            {
                Selector selector = new Selector();
            }
            catch (Exception ex)
            {
                Assert.True(false, "Should not throw execption here but it did. " + ex.Message);
            }
            Assert.True(true);
        }
        [Fact]
        public void TestNullPassedForTorrentContextToNextPiece()
        {
            UInt32 nextPiece = 0;
            Selector selector = new Selector();
            Assert.Throws<ArgumentNullException>(() => selector.NextPiece(null, ref nextPiece));
        }
        [Fact]
        public void TestNoPeersHavePieceOnCallToNextPiece()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            UInt32 nextPiece = 0;
            Selector selector = new Selector();
            Assert.False(selector.NextPiece(tc, ref nextPiece));
        }
        [Fact]
        public void TestAtLeastOnePeersHasPieceOnCallToNextPiece()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            UInt32 nextPiece = 0;
            Selector selector = new Selector();
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.IncrementPeerCount(pieceNumber);
            }
            Assert.True(selector.NextPiece(tc, ref nextPiece));
            Assert.True((nextPiece >= 0 && (nextPiece < tc.numberOfPieces)));
        }
        [Fact]
        public void TestNoMissingPiecesOnCallToNextPiece()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            UInt32 nextPiece = 0;
            Selector selector = new Selector();
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.IncrementPeerCount(pieceNumber);
                tc.MarkPieceMissing(pieceNumber, false);
            }
            Assert.False(selector.NextPiece(tc, ref nextPiece));
        }
        [Theory]
        [InlineData(6)]
        [InlineData(18)]
        [InlineData(14)]
        [InlineData(0)]
        [InlineData(16)]
        public void TestReturnCorrectLastMissingPieceOnCallToNextPiece(UInt32 expected)
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            UInt32 actual = 0;
            Selector selector = new Selector();
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.IncrementPeerCount(pieceNumber);
                tc.MarkPieceMissing(pieceNumber, false);
            }
            tc.MarkPieceMissing(expected, true);
            Assert.True(selector.NextPiece(tc, ref actual));
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void TestAllUniquePiecesChosenByNextPiece()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            HashSet<UInt32> pieces = new HashSet<UInt32>();
            Selector selector = new Selector();
            UInt32 nextPiece = 0;
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.IncrementPeerCount(pieceNumber);
            }
            while (selector.NextPiece(tc, ref nextPiece))
            {
                if ((nextPiece >= 0) && (nextPiece < tc.numberOfPieces))
                {
                    pieces.Add(nextPiece);
                    tc.MarkPieceMissing(nextPiece, false);
                }
            }
            Assert.False(selector.NextPiece(tc, ref nextPiece));
            Assert.Equal(tc.numberOfPieces, pieces.Count);
        }
        [Fact]
        public void TestPassNullAsPeerToLocalPieceSuggessions()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Selector selector = new Selector();
            Assert.Throws<ArgumentNullException>(() => selector.LocalPieceSuggestions(null, 10));
        }
        [Fact]
        public void TestCorrectNumberOfPiecesReturnedByLocalPieceSuggestions()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Selector selector = new Selector();
            Peer peer = new Peer("127.0.0.1", 6881, tc, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.IncrementPeerCount(pieceNumber);
                tc.MarkPieceLocal(pieceNumber, true);
            }
            UInt32[] pieces = selector.LocalPieceSuggestions(peer, 10);
            Assert.Equal(10, pieces.Length);
        }
        [Fact]
        public void TestAskTooManyPiecesFromLocalPeerSuggestions()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Selector selector = new Selector();
            Peer peer = new Peer("127.0.0.1", 6881, tc, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.IncrementPeerCount(pieceNumber);
                tc.MarkPieceLocal(pieceNumber, true);
            }
            UInt32[] pieces = selector.LocalPieceSuggestions(peer, 1000);
            Assert.Equal(tc.numberOfPieces, pieces.Length);
        }
        [Fact]
        public void TestAskForZeroPiecesFromLocalPeerSuggestions()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Selector selector = new Selector();
            Peer peer = new Peer("127.0.0.1", 6881, tc, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.IncrementPeerCount(pieceNumber);
                tc.MarkPieceLocal(pieceNumber, true);
            }
            UInt32[] pieces = selector.LocalPieceSuggestions(peer, 0);
            Assert.Empty(pieces);
        }
        [Fact]
        public void TestPassNullForTorrentContextToGetListOfPeers()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Manager manager = new Manager();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
            Selector selector = new Selector();
            Peer peer = new Peer("127.0.0.1", 6881, tc, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0));
            Assert.Throws<ArgumentNullException>(() => { Peer[] peers = selector.GetListOfPeers(null, 0, 10); });
        }
    }
}