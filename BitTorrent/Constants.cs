//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: BitTorrent related constants.
//
// Copyright 2019.
//

using System;

namespace BitTorrent
{
    public static class Constants
    {
        public const int kBlockSize = 1024*16;      // Client Block size
        public const int kHashLength = 20;          // Length of SHA1 hash in bytes
        public const byte kSizeOfUInt32 = 4;        // Number of bytes in wire protocol message length

        public const byte kMessageCHOKE = 0;        // Ids of wire protocol messages
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
