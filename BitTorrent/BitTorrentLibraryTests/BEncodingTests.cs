using System.Text;
//
// Author: Rob Tizzard
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
        [InlineData("d6:eBooksld7:edition5:third8:language6:Pascaled7:edition4:four8:language6:Pythoned7:edition6:second8:language3:SQLeee")]
        [InlineData("d4:bookld6:author15:Dennis Ritchie 7:edition5:First2:id3:4448:language1:Ced6:author19: Bjarne Stroustrup 7:edition6:second2:id3:5558:language3:C++eee")]
        [InlineData("d7:addressd4:city8:San Jone10:postalCode6:3942215:state2:CA13:streetAddress3:126e3:agei24e9:firstName4:Rack6:gender3:man8:lastName6:Jackon12:phoneNumbersld6:number10:73836276274:type4:homeeee")]
        [InlineData("d6:Actorsld9:Birthdate12:July 3, 19627:Born At12:Syracuse, NY3:agei56e4:name10:Tom Cruise5:photo44:https://jsonformatter.org/img/tom-cruise.jpged9:Birthdate13:April 4, 19657:Born At17:New York City, NY3:agei53e4:name17:Robert Downey Jr.5:photo50:https://jsonformatter.org/img/Robert-Downey-Jr.jpgeee")]
        [InlineData("d3:agei22e5:classl10:JavaScript4:HTML3:CSSe5:hobbyd5:sport8:footballe4:name4:Johne")]
        [InlineData("d4:codei0e12:commentCounti0e9:createdAt27:2020-01-02T13:32:16.748000611:description3:ghj2:idi2140e3:lati0e11:likeDisliked8:dislikesi0e5:likesi0e10:userActioni2ee3:lngi0e8:location39:Hermannplatz 5-6, 10967 Berlin, Germany9:mediatypei0e10:multiMediald8:createAt19:0001-01-01T00:00:002:idi3240e9:likeCounti0e9:mediatypei2e4:name0:3:url40:http://www.youtube.com/embed/mPhboJR0Llcee4:name5:manoj14:profilePicture47:Images/9b291404-bc2e-4806-88c5-08d29e65a5ad.png5:title2:gj6:userIdi4051ee")]
        public void TestDecodeThenEncodeTheSamString(string expected)
        {
            Bencode bEncode = new Bencode();
            BNodeBase bNode = bEncode.Decode(Encoding.ASCII.GetBytes(expected));
            byte[] actual = bEncode.Encode(bNode);
            Assert.Equal(Encoding.ASCII.GetBytes(expected), actual);
        }
    }
}