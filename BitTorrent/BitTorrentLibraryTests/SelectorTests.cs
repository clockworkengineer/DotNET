using System.Security.Cryptography;
//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent Torrent Context class.
//
// Copyright 2020.
//
using System;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class SelectorTests
    {
        [Fact]
        public void TestCreateSelector()
        {
            // MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            // file.Parse();
            // Manager manager = new Manager();
            // TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
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
            // MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            // file.Parse();
            // Manager manager = new Manager();
            // TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(manager), "/tmp");
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
    }
}