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
            Assert.Throws<IndexOutOfRangeException>(() => tc.MarkPieceLocal(1000, true));
        }
        [Fact]
        public void TestIsPieceLocalWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.IsPieceLocal(1000));
        }
        [Fact]
        public void TestMarkPieceMissingWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.MarkPieceMissing(1000, true));
        }
        [Fact]
        public void TestIsPieceMissingWithInvalidPieceNumber()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), "/tmp");
            Assert.Throws<IndexOutOfRangeException>(() => tc.IsPieceMissing(1000));
        }
    }
}