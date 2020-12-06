using System.ComponentModel.DataAnnotations;
using System;
namespace BitTorrentLibraryTests
{

  public  static class Constants
    {
        public const string SingleFileTorrent = "./files/singlefile.torrent";
        public const string MultiFileTorrent = "./files/multifile.torrent";
        public const string SingleFileWithErrorTorrent = "./files/singlefileerror.torrent";
        public const string MultiFileWithErrorTorrent = "./files/multifileerror.torrent";
        public const string DestinationDirectory = "/tmp";
        public const UInt32 InvalidPieceNumber=UInt32.MaxValue;
    }
}