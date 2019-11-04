using System;
using System.Net;
using System.Net.Sockets;

namespace BitTorrent
{
    public class Peer
    {   

        private string _ip;
        private int _port;
        private bool _peerChoking=true;
        private bool _amChoking=true;
        private Socket _peerSocket;
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
        public bool Connected { get => _connected; set => _connected = value; }
        public byte[] RemotePeerID { get => _remotePeerID; set => _remotePeerID = value; }

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
            _fileDownloader = fileDownloader;
            _readBuffer = new byte[Constants.kMessageLength];

        }

        public void connect()
        {
        
            IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
            IPEndPoint remotePeerIP = new IPEndPoint(localPeerIP, 0);

            _peerSocket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _peerSocket.Connect(_ip, _port);

            ValueTuple<bool, byte[]> peerResponse = PWP.intialHandshake(this, _infoHash);

            RemotePeerID = peerResponse.Item2;

            Connected = true;

            _peerSocket.BeginReceive(_readBuffer, 0, ReadBuffer.Length, 0, readPacketCallBack, this);

        }

        public void readPacketCallBack(IAsyncResult readAsyncState)
        {
            try
            {
                Peer remotePeer = (Peer)readAsyncState.AsyncState;
                int bytesRead = remotePeer._peerSocket.EndReceive(readAsyncState);
     
                remotePeer._bytesRead += bytesRead;

                if (!_lengthRead) {

                    if (remotePeer._bytesRead == Constants.kMessageLength)
                    {
                        UInt32 packetLength = 0;
                        packetLength = ((UInt32)remotePeer._readBuffer[0]) << 24;
                        packetLength |= ((UInt32)remotePeer._readBuffer[1]) << 16;
                        packetLength |= ((UInt32)remotePeer._readBuffer[2]) << 8;
                        packetLength |= ((UInt32)remotePeer._readBuffer[3]);
                        remotePeer._readBuffer = new byte[packetLength];
                        _lengthRead = true;
                        remotePeer._bytesRead = 0;
                        bytesRead = 0;
                    }
                }
                else if (remotePeer._bytesRead==remotePeer._readBuffer.Length) 
                {
                    PWP.remotePeerMessageProcess(remotePeer);
                    remotePeer._readBuffer = new byte[Constants.kMessageLength];
                    _lengthRead = false;
                    remotePeer._bytesRead = 0;
                    bytesRead = 0;

                }

                remotePeer._peerSocket.BeginReceive(remotePeer._readBuffer, remotePeer._bytesRead, 
                           remotePeer._readBuffer.Length - bytesRead, 0, readPacketCallBack, remotePeer);
            
            }
            catch (Exception e)
            {
                Program.Logger.Debug("ERROR : "+e.Message);
            }
        }

    }
}
