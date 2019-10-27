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
        private FileDownloader _fileDownloader;

        public bool PeerChoking { get => _peerChoking; set => _peerChoking = value; }
        public NetworkStream PeerStream { get => _peerStream; set => _peerStream = value; }
        public FileDownloader FileDownloader { get => _fileDownloader; set => _fileDownloader = value; }
        public bool AmChoking { get => _amChoking; set => _amChoking = value; }

        private void remotePeerReadMessages()
        {
            while (true)
            {
                PWP.readRemotePeerMessages(this, PeerStream);
            }
        }

        public Peer(FileDownloader fileDownloader, string ip ,int port, byte[] infoHash)
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
            FileDownloader = fileDownloader;

        }

        public void connect()
        {

            TcpClient peerClient = new TcpClient(_ip, _port);

            PeerStream = peerClient.GetStream();

            ValueTuple<bool, byte[]> peerResponse = PWP.intialHandshake(this, _infoHash);

            if (peerResponse.Item1)
            {
                Console.WriteLine($"BTP: Local Peer [{ PeerID.get()}] to remote peer [{Encoding.ASCII.GetString(peerResponse.Item2)}].");
            }

            _peerReadThread = new Thread(remotePeerReadMessages);
            _peerReadThread.Start();

        }
    }
}
