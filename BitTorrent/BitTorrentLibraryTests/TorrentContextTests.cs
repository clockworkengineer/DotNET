//
// Author: Robert Tizzard
//
// Description: XUnit tests for BiTorrent Torrent Context class.
//
// Copyright 2020.
//
using System;
using System.Net.Sockets;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class TorrentContextTests
    {
        [Fact]
        public void TestNullPassedForMetaInfoFileInCreationOfTorrentContext()
        {
            Assert.Throws<ArgumentNullException>(() => { TorrentContext tc = new TorrentContext(null, new Selector(), new DiskIO(new Manager()), "/tmp"); });
        }
        [Fact]
        public void TestNullPassedForSelectorInCreationOfTorrentContext()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Assert.Throws<ArgumentNullException>(() => { TorrentContext tc = new TorrentContext(file, null, new DiskIO(new Manager()), "/tmp"); });
        }
        [Fact]
        public void TestNullPassedForDiskIOInCreationOfTorrentContext()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Assert.Throws<ArgumentNullException>(() => { TorrentContext tc = new TorrentContext(file, new Selector(), null, "/tmp"); });
        }
        [Fact]
        public void TestNullPassedForDestinationDirectoryInCreationOfTorrentContext()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Assert.Throws<ArgumentException>(() => { TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), null); });
        }
        [Fact]
        public void TestSmptyStringPassedForDestinationDirectoryInCreationOfTorrentContext()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            Assert.Throws<ArgumentException>(() => { TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), ""); });
        }
        [Fact]
        public void TestUnparsedMetInfoFilePassInCreationOfTorrentContext()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => { TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp"); });
            Assert.Equal("BitTorrent Error: File has not been parsed.", error.Message);
        }
        [Fact]
        public void TestMarkPieceLocalWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.MarkPieceLocal(Constants.InvalidPieceNumber, true));
        }
        [Fact]
        public void TestIsPieceLocalWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.IsPieceLocal(Constants.InvalidPieceNumber));
        }
        [Fact]
        public void TestMarkPieceMissingWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.MarkPieceMissing(Constants.InvalidPieceNumber, true));
        }
        [Fact]
        public void TestIsPieceMissingWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.IsPieceMissing(Constants.InvalidPieceNumber));
        }
        [Fact]
        public void TestGetPieceLengthWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.GetPieceLength(Constants.InvalidPieceNumber));
        }
        [Fact]
        public void TestSetPieceLengthWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.SetPieceLength(Constants.InvalidPieceNumber, 0));
        }
        [Fact]
        public void TestSetPieceLengthLargerThanDefaultPieceSize()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            BitTorrentException error = Assert.Throws<BitTorrentException>(() => tc.SetPieceLength(0, UInt32.MaxValue));
            Assert.Equal("BitTorrent Error: Piece length larger than maximum for torrent.", error.Message);
        }
        [Fact]
        public void TestNullPassedToIsSpaceInSwarm()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<ArgumentException>(() => tc.IsSpaceInSwarm(null));
        }
        [Fact]
        public void TestEmptyStringPassedToIsSpaceInSwarm()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<ArgumentException>(() => tc.IsSpaceInSwarm(""));
        }
        [Fact]
        public void TestInvalidPieceNumberPassedToIncrementPieceCount()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.IncrementPeerCount(Constants.InvalidPieceNumber));
        }
    }
}