using System.ComponentModel.DataAnnotations;
using System;
namespace BitTorrentLibraryTests
{

  public  static class Constants
    {
        public const string SingleFileTorrent = "singlefile.torrent";
        public const string MultiFileTorrent = "multifile.torrent";
        public const string SingleFileWithErrorTorrent = "singlefileerror.torrent";
        public const string MultiFileWithErrorTorrent = "multifileerror.torrent";
        public const UInt32 InvalidPieceNumber=UInt32.MaxValue;
    }
}