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
            Bencode bEncode = new Bencode();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileTorrent);
            BNodeBase torrentBase = bEncode.Decode(expected);
            byte[] actual = bEncode.Encode(torrentBase);
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void TestMultiFileTorrentDecodeEncodeCheckTheSameAfter()
        {
            Bencode bEncode = new Bencode();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileTorrent);
            BNodeBase torrentBase = bEncode.Decode(expected);
            byte[] actual = bEncode.Encode(torrentBase);
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void TestSingleFileTorrentWithErrorDecode()
        {
            Bencode bEncode = new Bencode();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileWithErrorTorrent);
            Assert.Throws<Exception>(() => { BNodeBase torrentBase = bEncode.Decode(expected); });
        }
        [Fact]
        public void TestMultiFileTorrentWithErrorDecode()
        {
            Bencode bEncode = new Bencode();
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileWithErrorTorrent);
            Assert.Throws<Exception>(() => { BNodeBase torrentBase = bEncode.Decode(expected); });
        }
    }
}