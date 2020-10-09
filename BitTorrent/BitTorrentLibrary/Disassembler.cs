//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: 
//
// Copyright 2020.
//

using System;
using System.Threading;

namespace BitTorrentLibrary
{
    public class Disassembler
    {
        public UInt32 ActiveDisassemblerTasks { get; set; }
        public Disassembler(Downloader downloader)
        {

        }

        public void DisassemlePieces(Peer remotePeer)
        {

            ActiveDisassemblerTasks++;

            while(true) {
                Thread.Sleep(1000);
            }
            ActiveDisassemblerTasks--;

        }
    }
}
