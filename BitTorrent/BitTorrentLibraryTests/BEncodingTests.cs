using System;
using System.Text;
using Xunit;
using BitTorrent;

namespace BitTorrentLibraryTests
{
    public class BEncodingTests
    {
        [Fact]
        public void testSingleFileTorrentDecodeEncodeCheckTheSameAfter()
        {
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileTorrent);

            BNodeBase torrentBase = Bencoding.Decode(expected);

            byte[] actual = Bencoding.Encode(torrentBase);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void testMultiFileTorrentDecodeEncodeCheckTheSameAfter()
        {
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileTorrent);

            BNodeBase torrentBase = Bencoding.Decode(expected);

            byte[] actual = Bencoding.Encode(torrentBase);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void testSingleFileTorrentWithErrorDecode()
        {
            byte[] expected = System.IO.File.ReadAllBytes(Constants.SingleFileWithErrorTorrent);

            Assert.Throws<Error>(() =>  { BNodeBase torrentBase = Bencoding.Decode(expected); });

        }

        [Fact]
        public void testMultiFileTorrentWithErrorDecode()
        {
            byte[] expected = System.IO.File.ReadAllBytes(Constants.MultiFileWithErrorTorrent);

            Assert.Throws<Error>(() =>  { BNodeBase torrentBase = Bencoding.Decode(expected); });

        }
    }
}