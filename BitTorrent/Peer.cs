using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BitTorrent
{
    public class Peer
    {   
        private string _ip;
        private int _port;
        private bool _peerChoking=true;
        private bool _amChoking=true;
        private NetworkStream _peerStream;
        private readonly byte[] _protocolName;
        private byte[] _infoHash;
        private bool _connected = false;
        private byte[] _remotePeerID;
        private Thread _peerReadThread;

        private void processRemotePeerRead(byte[] buffer, Int32 length)
        {

            switch (buffer[0])
            {
                case 0x0:
                    Console.WriteLine("CHOKE");
                    break;
                case 0x1:
                    Console.WriteLine("UNCHOKE");
                    break;
                case 0x2:
                    Console.WriteLine("INTERESTED");
                    break;
                case 0x3:
                    Console.WriteLine("UNINTERESTED");
                    break;
                case 0x4:
                    Console.WriteLine("HAVE");
                    break;
                case 0x5:
                    Console.WriteLine("BITFIELD");
                    break;
                case 0x6:
                    Console.WriteLine("REQUEST");
                    break;
                case 0x7:
                    Console.WriteLine("PIECE");
                    break;
                case 0x8:
                    Console.WriteLine("CANCEL");
                    break;
                default:
                    Console.WriteLine("UNKOWN");
                    break;
            }
            Thread.Sleep(1000);
        }

        private void performPeerThreadRead()
        {
            byte[] messageLength = new byte[4];
            Int32 convertedLength = 0;

            while (true)
            {
                PWP.processRemotePeerRead(_peerStream);

            }
        }

        public Peer(string ip ,int port, byte[] infoHash)
        {
            if (ip.Contains(":"))
            {
                _ip = ip.Substring(ip.LastIndexOf(":") + 1);
            }
            else
            {
                _ip = ip;
            }
            _port = port;
            _infoHash = infoHash;
       
        }

        public void connect()
        {

            TcpClient peerClient = new TcpClient(_ip, _port);

            _peerStream = peerClient.GetStream();

            ValueTuple<bool, byte[]> peerResponse = PWP.intialHandshake(_peerStream, _infoHash);

            if (peerResponse.Item1)
            {
                Console.WriteLine($"BTP: Local Peer [{ PeerID.get()}] to remote peer [{Encoding.ASCII.GetString(peerResponse.Item2)}].");
            }

            _peerReadThread = new Thread(performPeerThreadRead);
            _peerReadThread.Start();

        }
    }
}
