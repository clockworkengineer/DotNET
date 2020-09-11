using System;
using System.Text;
using Xunit;
using BitTorrent;

namespace BitTorrentLibraryTests
{
    public class MetaFileInfoTests
    {
        const string torrentFile01 = "singlefile.torrent";
        const string torrentFile02 = "multifile.torrent";

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

        [Theory]
        [InlineData("announce", "http://192.168.1.215:9005/announce")]
        //[InlineData("announce-list","")]
        [InlineData("comment", "Just a large jpeg image torrent.")]
        [InlineData("created by", "qBittorrent v4.1.5")]
        [InlineData("creation date", "1599750678")]
        [InlineData("name", "large.jpeg")]
        [InlineData("piece length", "16384")]
        //[InlineData("pieces","")]
        [InlineData("private", "1")]
        [InlineData("url-list", "http://192.168.1.215:9005/announce")]
        //[InlineData("files", "")]
        [InlineData("length", "351874")]
        //[InlineData("md5sum", "")]
        //[InlineData("info hash", "")]
        public void testSingleFileTorrentCheckKeyContents(string key, string expected)
        {

            MetaInfoFile torrentFile = new MetaInfoFile(torrentFile01);

            torrentFile.Load();
            torrentFile.Parse();

            string actual = System.Text.Encoding.UTF8.GetString(torrentFile.MetaInfoDict[key]);

            Assert.Equal(actual, expected);

        }

        [Theory]
        [InlineData(torrentFile01, "7fd1a2631b385a4cc68bf15040fa375c8e68cb7e")]
        [InlineData(torrentFile02, "c28bf4c5ab095923eecad46701d09408912928e7")]
        public void testTorrentCheckInfoHash(string file, string expected)
        {
            MetaInfoFile torrentFile = new MetaInfoFile(file);

            torrentFile.Load();
            torrentFile.Parse();

            byte[] infoHash = torrentFile.MetaInfoDict["info hash"];

            StringBuilder actual = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
            {
                actual.AppendFormat("{0:x2}", b);
            }

            Assert.Equal(actual.ToString(), expected);

        }

    }
}
