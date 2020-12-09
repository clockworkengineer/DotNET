//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent PieceBuffer class.
//
// Copyright 2020.
//
using System;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class PieceBufferTests
    {
        [Fact]
        public void TestNullPassedForManagerToPieceBuffer()
        {
            Assert.Throws<ArgumentNullException>(() => { PieceBuffer piece = new PieceBuffer(null, BitTorrentLibrary.Constants.BlockSize); });
        }
        [Fact]
        public void TestPassNullForBufferToAddBlockFromPacket()
        {
            MetaInfoFile file = new MetaInfoFile(Constants.SingleFileTorrent);
            file.Parse();
            TorrentContext tc = new TorrentContext(file, new Selector(), new DiskIO(new Manager()), Constants.DestinationDirectory);
            PieceBuffer pieceBuffer = new PieceBuffer(tc, BitTorrentLibrary.Constants.BlockSize);
            Assert.Throws<ArgumentNullException> (()=> pieceBuffer.AddBlockFromPacket(null, BitTorrentLibrary.Constants.BlockSize));
        }
    }
}