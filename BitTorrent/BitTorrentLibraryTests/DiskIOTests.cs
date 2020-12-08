//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent DiskIO class.
//
// Copyright 2020.
//
using System;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class DiskIOTests
    {
        [Fact]
        public void TestNullPassedForManagerToDiskIO()
        {
            Assert.Throws<ArgumentNullException>(() => { DiskIO diskIO = new DiskIO(null); });
        }
        [Fact]
        public void TestNullPassedForTorrentConextToCreateLocalTorrentStructure()
        {
            DiskIO diskIO = new DiskIO(new Manager());
            Assert.Throws<ArgumentNullException>(() => diskIO.CreateLocalTorrentStructure(null));
        }
        [Fact]
        public void TestNullPassedForTorrentConextToCreateTorrentBitfield()
        {
            DiskIO diskIO = new DiskIO(new Manager());
            Assert.Throws<ArgumentNullException>(() => diskIO.CreateTorrentBitfield(null));
        }
        [Fact]
        public void TestNullPassedForTorrentConextToFullyDownloadedTorrentBitfield()
        {
            DiskIO diskIO = new DiskIO(new Manager());
            Assert.Throws<ArgumentNullException>(() => diskIO.FullyDownloadedTorrentBitfield(null));
        }
    }
}