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
        private Socket _peerSocket;
        private readonly byte[] _protocolName;
        private byte[] _infoHash;
        private bool _connected = false;
        private byte[] _remotePeerID;
        private FileDownloader _fileDownloader;
        private bool _readFromRemotePeer = true;
        private byte[] _readBuffer;
        private int _bytesRead = 0;
        private bool _lengthRead = false;

        public bool PeerChoking { get => _peerChoking; set => _peerChoking = value; }
        public FileDownloader FileDownloader { get => _fileDownloader; set => _fileDownloader = value; }
        public bool AmChoking { get => _amChoking; set => _amChoking = value; }
        public bool ReadFromRemotePeer { get => _readFromRemotePeer; set => _readFromRemotePeer = value; }
        public Socket PeerSocket { get => _peerSocket; set => _peerSocket = value; }
        public byte[] ReadBuffer { get => _readBuffer; set => _readBuffer = value; }
        public int BytesRead { get => _bytesRead; set => _bytesRead = value; }

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
            ReadBuffer = new byte[Constants.kMessageLength];

        }

        public void connect()
        {
        
            IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
            IPEndPoint remotePeerIP = new IPEndPoint(localPeerIP, 0);

            PeerSocket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            PeerSocket.Connect(_ip, _port);

            ValueTuple<bool, byte[]> peerResponse = PWP.intialHandshake(this, _infoHash);

            if (peerResponse.Item1)
            {
                Program.Logger.Debug($"BTP: Local Peer [{ PeerID.get()}] to remote peer [{Encoding.ASCII.GetString(peerResponse.Item2)}].");
            }

            PeerSocket.BeginReceive(_readBuffer, 0, ReadBuffer.Length, 0, readPacketCallBack, this);

        }

        public void readPacketCallBack(IAsyncResult readAsyncState)
        {
            try
            {
                Peer remotePeer = (Peer)readAsyncState.AsyncState;
                int bytesRead = remotePeer.PeerSocket.EndReceive(readAsyncState);

                remotePeer.BytesRead += bytesRead;

                if (!_lengthRead) {

                    if (remotePeer.BytesRead == Constants.kMessageLength)
                    {
                        UInt32 packetLength = 0;
                        packetLength = ((UInt32)remotePeer.ReadBuffer[0]) << 24;
                        packetLength |= ((UInt32)remotePeer.ReadBuffer[1]) << 16;
                        packetLength |= ((UInt32)remotePeer.ReadBuffer[2]) << 8;
                        packetLength |= ((UInt32)remotePeer.ReadBuffer[3]);
                        remotePeer.ReadBuffer = new byte[packetLength];
                        _lengthRead = true;
                        remotePeer.BytesRead = 0;
                        bytesRead = 0;
                    }
                }
                else if (remotePeer.BytesRead==remotePeer.ReadBuffer.Length) 
                {
                    PWP.remotePeerMessageProcess(remotePeer);
                    remotePeer.ReadBuffer = new byte[Constants.kMessageLength];
                    _lengthRead = false;
                    remotePeer.BytesRead = 0;
                    bytesRead = 0;

                }

                remotePeer.PeerSocket.BeginReceive(remotePeer.ReadBuffer, remotePeer.BytesRead, 
                           remotePeer.ReadBuffer.Length - bytesRead, 0, readPacketCallBack, remotePeer);
            
            }
            catch (Exception e)
            {
                Program.Logger.Debug("ERROR : "+e.Message);
            }
        }

    }
}
