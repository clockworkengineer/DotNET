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
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileTorrent);
            BNodeBase torrentBase = Bencoding.Decode(expected);
            byte[] actual = Bencoding.Encode(torrentBase);
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void TestMultiFileTorrentDecodeEncodeCheckTheSameAfter()
        {
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileTorrent);
            BNodeBase torrentBase = Bencoding.Decode(expected);
            byte[] actual = Bencoding.Encode(torrentBase);
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void TestSingleFileTorrentWithErrorDecode()
        {
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileWithErrorTorrent);
            Assert.Throws<Exception>(() =>  { BNodeBase torrentBase = Bencoding.Decode(expected); });
        }
        [Fact]
        public void TestMultiFileTorrentWithErrorDecode()
        {
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileWithErrorTorrent);
            Assert.Throws<Exception>(() =>  { BNodeBase torrentBase = Bencoding.Decode(expected); });
        }
    }
}