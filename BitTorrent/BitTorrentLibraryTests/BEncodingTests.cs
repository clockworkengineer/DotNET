//
// Author: Robert Tizzard
//
// Description: XUnit tests for BiTorrent BEncoding class.
//
// Copyright 2020.
//
using System;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class BEncodingTests
    {
        [Fact]
        public void TestSingleFileTorrentDecodeEncodeCheckTheSameAfter()
        {
            BEncoding bEncoding = new BEncoding();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileTorrent);
            BNodeBase torrentBase = bEncoding.Decode(expected);
            byte[] actual = bEncoding.Encode(torrentBase);
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void TestMultiFileTorrentDecodeEncodeCheckTheSameAfter()
        {
            BEncoding bEncoding = new BEncoding();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileTorrent);
            BNodeBase torrentBase = bEncoding.Decode(expected);
            byte[] actual = bEncoding.Encode(torrentBase);
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void TestSingleFileTorrentWithErrorDecode()
        {
            BEncoding bEncoding = new BEncoding();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileWithErrorTorrent);
            Assert.Throws<Exception>(() => { BNodeBase torrentBase = bEncoding.Decode(expected); });
        }
        [Fact]
        public void TestMultiFileTorrentWithErrorDecode()
        {
            BEncoding bEncoding = new BEncoding();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileWithErrorTorrent);
            Assert.Throws<Exception>(() => { BNodeBase torrentBase = bEncoding.Decode(expected); });
        }
    }
}