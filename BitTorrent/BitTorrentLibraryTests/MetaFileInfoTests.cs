using System;
using System.Text;
using Xunit;
using BitTorrentLibrary;

namespace BitTorrentLibraryTests
{
    public class MetaFileInfoTests
    {
        [Fact]
        public void TestExceptionOnFileNotExisting()
        {

            Assert.Throws<BitTorrentError>(() =>  { MetaInfoFile torrentFile = new MetaInfoFile("s" + Constants.SingleFileTorrent);} );
        }

        [Theory]
        [InlineData("announce")]
        [InlineData("comment")]
        [InlineData("created by")]
        [InlineData("creation date")]
        [InlineData("name")]
        [InlineData("piece length")]
        [InlineData("pieces")]
        [InlineData("private")]
        [InlineData("url-list")]
        [InlineData("length")]
        [InlineData("info hash")]
         public void TestSingleFileTorrentContainsValidKey(string key)
        {
            MetaInfoFile torrentFile = new MetaInfoFile(Constants.SingleFileTorrent);

            torrentFile.Parse();

            Assert.True(torrentFile.MetaInfoDict.ContainsKey(key));
        }

        [Theory]
        [InlineData("announce")]
        [InlineData("comment")]
        [InlineData("created by")]
        [InlineData("creation date")]
        [InlineData("name")]
        [InlineData("piece length")]
        [InlineData("pieces")]
        [InlineData("private")]
        [InlineData("url-list")]
        [InlineData("info hash")]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("2")]
        [InlineData("3")]
        [InlineData("4")]
        public void TestMultiFileTorrentContainsValidKey(string key)
        {
            MetaInfoFile torrentFile = new MetaInfoFile(Constants.MultiFileTorrent);

            torrentFile.Parse();

            Assert.True(torrentFile.MetaInfoDict.ContainsKey(key));
        }

        [Theory]
        [InlineData("announce", "http://192.168.1.215:9005/announce")]
        [InlineData("comment", "Just a large jpeg image torrent.")]
        [InlineData("created by", "qBittorrent v4.1.5")]
        [InlineData("creation date", "1599750678")]
        [InlineData("name", "large.jpeg")]
        [InlineData("piece length", "16384")]
        [InlineData("private", "1")]
        [InlineData("url-list", "http://192.168.1.215:9005/announce")]
        [InlineData("length", "351874")]
        public void TestSingleFileTorrentCheckKeyContents(string key, string expected)
        {
            MetaInfoFile torrentFile = new MetaInfoFile(Constants.SingleFileTorrent);
            
            torrentFile.Parse();

            string actual = System.Text.Encoding.UTF8.GetString(torrentFile.MetaInfoDict[key]);

            Assert.Equal(actual, expected);
        }

        [Theory]
        [InlineData("announce","http://192.168.1.215:9005/announce")]
        [InlineData("comment","Just a large jpeg image torrent.")]
        [InlineData("created by","qBittorrent v4.1.5")]
        [InlineData("creation date","1599752851")]
        [InlineData("name","Tester")]
        [InlineData("piece length","16384")]
        [InlineData("private","1")]
        [InlineData("url-list","http://192.168.1.215:9005/announce")]
        [InlineData("0","/large.jpeg,351874,")]
        [InlineData("1","/2,100,")]
        [InlineData("2","/1,88,")]
        [InlineData("3","/large1.jpeg,351874,")]
        [InlineData("4","/large2.jpeg,351874,")]
        public void TestMultiFileTorrentCheckKeyContents(string key, string expected)
        {
            MetaInfoFile torrentFile = new MetaInfoFile(Constants.MultiFileTorrent);

            torrentFile.Parse();

            string actual = System.Text.Encoding.UTF8.GetString(torrentFile.MetaInfoDict[key]);

            Assert.Equal(actual, expected);
        }

        [Theory]
        [InlineData(Constants.SingleFileTorrent, "7fd1a2631b385a4cc68bf15040fa375c8e68cb7e")]
        [InlineData(Constants.MultiFileTorrent, "c28bf4c5ab095923eecad46701d09408912928e7")]
        public void TestTorrentCheckInfoHash(string file, string expected)
        {
            MetaInfoFile torrentFile = new MetaInfoFile(file);

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
