using System;
using Xunit;
using BitTorrent;

namespace BitTorrentLibraryTests
{
    public class MetaFileInfoTests
    {
        const string torrentFile01 = "test01.torrent";
        const string torrentFile02 = "test02.torrent";

        [Fact]
        public void testExceptionOnFileNotExisting()
        {

            MetaInfoFile torrentFile = new MetaInfoFile("s" + torrentFile01);

            Assert.Throws<Error>(() => { torrentFile.Load(); });

        }

        [Theory]
        [InlineData("announce")]
        //[InlineData("announce-list")]
        [InlineData("comment")]
        [InlineData("created by")]
        [InlineData("creation date")]
        [InlineData("name")]
        [InlineData("piece length")]
        [InlineData("pieces")]
        [InlineData("private")]
        [InlineData("url-list")]
        //[InlineData("files")]
        [InlineData("length")]
        //[InlineData("md5sum")]
        [InlineData("info hash")]
        public void testSingleFileTorrentContainsValidKey(string key)
        {

            MetaInfoFile torrentFile = new MetaInfoFile(torrentFile01);

            torrentFile.Load();
            torrentFile.Parse();

            Assert.True(torrentFile.MetaInfoDict.ContainsKey(key));

        }

        [Theory]
        [InlineData("announce")]
        // [InlineData("announce-list")]
        [InlineData("comment")]
        [InlineData("created by")]
        [InlineData("creation date")]
        [InlineData("name")]
        [InlineData("piece length")]
        [InlineData("pieces")]
        [InlineData("private")]
        [InlineData("url-list")]
         // [InlineData("files")]
        // [InlineData("length")]
        // [InlineData("md5sum")]
        [InlineData("info hash")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("2")]
        [InlineData("3")]
        [InlineData("4")]
        public void testMultiFileTorrentContainsValidKey(string key)
        {

            MetaInfoFile torrentFile = new MetaInfoFile(torrentFile02);

            torrentFile.Load();
            torrentFile.Parse();

            Assert.True(torrentFile.MetaInfoDict.ContainsKey(key));

        }

    }
}
