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
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace BitTorrentLibrary
{
    public class Uploader
    {

        private static readonly byte[] _protocolName = Encoding.ASCII.GetBytes("BitTorrent protocol");
        private Socket socket;

        private byte[] _peerID;
        private byte[] _infoHash;

        public Uploader()
        {

        }

        public int PeerRead(byte[] buffer, int length)
        {
            return socket.Receive(buffer, length, SocketFlags.None);
        }

        public void Upload()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Host.GetIP());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipHostInfo.AddressList[0], (int)Host.DefaultPort);

            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(localEndPoint);
            listener.Listen(100);

            Log.Logger.Info("Waiting for remote peer connect...");

            socket = listener.Accept();

            Log.Logger.Info("Remote peer connected...");

            string remotePeerIP = ((IPEndPoint)(socket.RemoteEndPoint)).Address.ToString();

            Log.Logger.Info($"Remote peer IP = {remotePeerIP}");

            _infoHash = new byte[20];
            _peerID = new byte[20];
            byte[] intialPacket = new byte[_protocolName.Length+1+8+_infoHash.Length+_peerID.Length];

            var bytesRead = PeerRead(intialPacket, intialPacket.Length);

            socket.Close();

        }

    }
}
