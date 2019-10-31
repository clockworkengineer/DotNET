using System;
namespace BitTorrent
{
    public static class Constants
    {
        public const int kBlockSize = 1024;
        public const int kHashLength = 20;
        public const byte kMessageLength = 4;
        public const byte kMessageCHOKE = 0;
        public const byte kMessageUNCHOKE = 1;
        public const byte kMessageINTERESTED = 2;
        public const byte kMessageUNINTERESTED = 3;
        public const byte kMessageHAVE = 4;
        public const byte kMessageBITFIELD = 5;
        public const byte kMessageREQUEST = 6;
        public const byte kMessagePIECE = 7;
        public const byte kMessageCANCEL = 8;
    }
}
