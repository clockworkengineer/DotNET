//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The Uploader class encapsulates all code and
// data relating to the uploading of pieces of a torrent to a remote
// peer. This includes the selection and reding of the relevant pieces
// of the torrent from disk, packaging it and sending it to the remote
// peers.
//
// Copyright 2019.
//

using System;
using System.Net;
using System.Net.Sockets;

namespace BitTorrentLibrary
{
    public class Uploader
    {
        Uploader()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Host.GetIP());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipHostInfo.AddressList[0], (int)Host.DefaultPort);

            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(localEndPoint);
            listener.Listen(100);
        }

    }
}
