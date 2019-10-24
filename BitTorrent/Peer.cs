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

        private bool validatePeerConnect(byte[] handshakePacket, byte[]handshakeResponse)
        {

            for (int byteNumber = 0; byteNumber < _protocolName.Length + 1; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return(false);
                }
            }
            for (int byteNumber = _protocolName.Length + 9; byteNumber < _protocolName.Length + 29; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return(false);
                }
            }

           _remotePeerID = new byte[20];

            Buffer.BlockCopy(handshakeResponse, _protocolName.Length + 29, _remotePeerID, 0, _remotePeerID.Length);

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
            List<byte> handshakePacket = new List<byte>();

            handshakePacket.Add((byte)_protocolName.Length);
            handshakePacket.AddRange(_protocolName);
            handshakePacket.AddRange(new byte[8]);
            handshakePacket.AddRange(_infoHash);
            handshakePacket.AddRange(Encoding.ASCII.GetBytes(PeerID.get()));

            _peerStream.Write(handshakePacket.ToArray(), 0, handshakePacket.Count);

            byte[] handshakeResponse = new byte[handshakePacket.Count];

            _peerStream.Read(handshakeResponse, 0, handshakeResponse.Length);

            _connected = validatePeerConnect(handshakePacket.ToArray(), handshakeResponse);

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
