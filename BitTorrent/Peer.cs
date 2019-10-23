using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

        private bool validatePeerConnect(byte[] initialPacket, byte[]initialResponse)
        {

            for (int byteNumber = 0; byteNumber < _protocolName.Length + 1; byteNumber++)
            {
                if (initialPacket[byteNumber] != initialResponse[byteNumber])
                {
                    return(false);
                }
            }
            for (int byteNumber = _protocolName.Length + 9; byteNumber < _protocolName.Length + 29; byteNumber++)
            {
                if (initialPacket[byteNumber] != initialResponse[byteNumber])
                {
                    return(false);
                }
            }

           _remotePeerID = new byte[20];

            Buffer.BlockCopy(initialResponse, _protocolName.Length + 29, _remotePeerID, 0, _remotePeerID.Length);

            return (true);

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
            _protocolName = Encoding.ASCII.GetBytes("BitTorrent protocol");
            
        }

        private void performIntialHandshake()
        {
            List<byte> initialPacket = new List<byte>();

            initialPacket.Add((byte)_protocolName.Length);
            initialPacket.AddRange(_protocolName);
            initialPacket.AddRange(new byte[8]);
            initialPacket.AddRange(_infoHash);
            initialPacket.AddRange(Encoding.ASCII.GetBytes(PeerID.get()));

            _peerStream.Write(initialPacket.ToArray(), 0, initialPacket.Count);

            byte[] initialResponse = new byte[initialPacket.Count];

            _peerStream.Read(initialResponse, 0, initialResponse.Length);

            _connected = validatePeerConnect(initialPacket.ToArray(), initialResponse);

            if (_connected)
            { 
                Console.WriteLine($"BTP: Local Peer [{ PeerID.get()}] to remote peer [{Encoding.ASCII.GetString(_remotePeerID)}].");
            }


        }

        public void connect()
        {

            TcpClient peerClient = new TcpClient(_ip, _port);

            _peerStream = peerClient.GetStream();

            performIntialHandshake();
 
        }
    }
}
