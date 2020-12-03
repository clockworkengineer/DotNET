using System.Text;
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
        [Theory]
        [InlineData("d1:0d5:email22:jpenddreth0@census.gov10:first_name8:Jeanette6:gender6:Female2:idi1e10:ip_address11:26.58.193.29:last_name9:Penddrethe1:1d5:email21:gfrediani1@senate.gov10:first_name7:Giavani6:gender4:Male2:idi2e10:ip_address13:229.179.4.2129:last_name8:Fredianie1:2d5:email19:nbea2@imageshack.us10:first_name5:Noell6:gender6:Female2:idi3e10:ip_address14:180.66.162.2559:last_name3:Beae1:3d5:email14:wvalek3@vk.com10:first_name7:Willard6:gender4:Male2:idi4e10:ip_address12:67.76.188.269:last_name5:Valekee")]
        [InlineData("d27:DestinationTorrentDirectory19:/home/robt/utorrent17:SeedFileDirectory79:/home/robt/Projects/dotNET/BitTorrent/ClientUI/bin/Debug/netcoreapp3.1/seeding/20:TorrentFileDirectory18:/home/robt/torrente")]
        [InlineData("d8:glossaryd8:GlossDivd9:GlossListd10:GlossEntryd6:Abbrev13:ISO 8879:19867:Acronym4:SGML8:GlossDefd12:GlossSeeAlsol3:GML3:XMLe4:para72:A meta-markup language, used to create markup languages such as DocBook.e8:GlossSee6:markup9:GlossTerm36:Standard Generalized Markup Language2:ID4:SGML6:SortAs4:SGMLee5:title1:Se5:title16:example glossaryee")]
        public void TestDecodeThenEncodeTheSamString(string bencode)
        {
            Bencode bEncode = new Bencode();
            BNodeBase bNode = bEncode.Decode(Encoding.ASCII.GetBytes(bencode));
            byte[] actual = bEncode.Encode(bNode);
            Assert.Equal(Encoding.ASCII.GetBytes(bencode), actual);
        }
    }
}