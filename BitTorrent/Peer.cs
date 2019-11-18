//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: 
//
// Copyright 2019.
//
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BitTorrent
{
    public static class SocketExtensions
    {
        /// <summary>
        /// Connects the specified socket.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="endpoint">The IP endpoint.</param>
        /// <param name="timeout">The timeout.</param>
        public static void Connect(this Socket socket, EndPoint endpoint, TimeSpan timeout)
        {
            var result = socket.BeginConnect(endpoint, null, null);

            bool success = result.AsyncWaitHandle.WaitOne(timeout, true);
            if (success)
            {
                socket.EndConnect(result);
            }
            else
            {
                socket.Close();
                throw new Error("Error: Peer connect timed out.");
            }
        }
    }
    public class Peer
    {  
        private string _ip;
        private UInt32 _port;
        private ManualResetEvent _peerChoking;
        private bool _interested = true;
        private Socket _peerSocket;
        private byte[] _infoHash;
        private bool _connected = false;
        private byte[] _remotePeerID;
        private FileDownloader _torrentDownloader;
        private PieceBuffer _assembledPiece;
        private byte[] _readBuffer;
        private UInt32 _bytesRead = 0;
        private UInt32 _packetLength = 0;
        private bool _lengthRead = false;
        private ManualResetEvent _bitfieldReceived;
        private byte[] _remotePieceBitfield;
        private ManualResetEvent _waitForPieceAssembly;

        public Socket PeerSocket { get => _peerSocket; set => _peerSocket = value; }
        public byte[] ReadBuffer { get => _readBuffer; set => _readBuffer = value; }
        public bool Connected { get => _connected; set => _connected = value; }
        public byte[] RemotePeerID { get => _remotePeerID; set => _remotePeerID = value; }
        public FileDownloader TorrentDownloader { get => _torrentDownloader; set => _torrentDownloader = value; }
        public bool Interested { get => _interested; set => _interested = value; }
        public byte[] RemotePieceBitfield { get => _remotePieceBitfield; set => _remotePieceBitfield = value; }
        public uint PacketLength { get => _packetLength; set => _packetLength = value; }
        public PieceBuffer AssembledPiece { get => _assembledPiece; set => _assembledPiece = value; }
        public ManualResetEvent WaitForPieceAssembly { get => _waitForPieceAssembly; set => _waitForPieceAssembly = value; }
        public ManualResetEvent PeerChoking { get => _peerChoking; set => _peerChoking = value; }
        public ManualResetEvent BitfieldReceived { get => _bitfieldReceived; set => _bitfieldReceived = value; }

        private void ReadPacketCallBack(IAsyncResult readAsyncState)
        {
            Peer remotePeer = (Peer)readAsyncState.AsyncState;

            try
            {

                UInt32 bytesRead = (UInt32)remotePeer._peerSocket.EndReceive(readAsyncState);

                remotePeer._bytesRead += bytesRead;

                if (!remotePeer._lengthRead)
                {
                    if (remotePeer._bytesRead == Constants.kSizeOfUInt32)
                    {
                        remotePeer.PacketLength = ((UInt32)remotePeer._readBuffer[0]) << 24;
                        remotePeer.PacketLength |= ((UInt32)remotePeer._readBuffer[1]) << 16;
                        remotePeer.PacketLength |= ((UInt32)remotePeer._readBuffer[2]) << 8;
                        remotePeer.PacketLength |= ((UInt32)remotePeer._readBuffer[3]);
                        remotePeer._lengthRead = true;
                        remotePeer._bytesRead = 0;
                    }
                }
                else if (remotePeer._bytesRead == remotePeer.PacketLength)
                {
                    PWP.RemotePeerMessageProcess(remotePeer);
                    remotePeer._lengthRead = false;
                    remotePeer._bytesRead = 0;
                    remotePeer.PacketLength = Constants.kSizeOfUInt32;
                }

                remotePeer._peerSocket.BeginReceive(remotePeer._readBuffer, (Int32)remotePeer._bytesRead,
                           (Int32)(remotePeer.PacketLength - remotePeer._bytesRead), 0, ReadPacketCallBack, remotePeer);

            }
            catch (Error)
            {
                throw;
            }
            catch (System.ObjectDisposedException)
            {
                Program.Logger.Info($"Packet read for Peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} closed.");
            }
            catch (Exception ex)
            {
                Program.Logger.Debug("Internal ReadPacketCallBack() error : " + ex.Message);
                Program.Logger.Debug(ex);
            }
        }

        static public string GetLocalHostIP()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Error("No network adapters with an IPv4 address in the system!");
        }

        public Peer(FileDownloader fileDownloader, string ip ,UInt32 port, byte[] infoHash)
        {
            _ip = ip;
            _port = port;
            _infoHash = infoHash;
            _torrentDownloader = fileDownloader;
            _readBuffer = new byte[Constants.kBlockSize + (2*Constants.kSizeOfUInt32) + 1]; // Maximum possible packet size
            _assembledPiece = new PieceBuffer(fileDownloader.Dc.pieceLength);
            _waitForPieceAssembly = new ManualResetEvent(false);
            _peerChoking = new ManualResetEvent(false);
            _bitfieldReceived = new ManualResetEvent(false);
        }

        public void Connect()
        {

            try
            {
                IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
                IPAddress remotePeerIP = System.Net.IPAddress.Parse(_ip);

                _peerSocket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                _peerSocket.Connect(new IPEndPoint(remotePeerIP, (Int32)_port), new TimeSpan(0, 0, Constants.kReadSocketTimeout));

                ValueTuple<bool, byte[]> peerResponse = PWP.intialHandshake(this, _infoHash);

                if (peerResponse.Item1)
                {
                    RemotePeerID = peerResponse.Item2;
                    _connected = true;
                    _peerSocket.BeginReceive(_readBuffer, 0, Constants.kSizeOfUInt32, 0, ReadPacketCallBack, this);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

        }

        public bool IsPieceOnRemotePeer(UInt32 pieceNumber)
        {
            return ((_remotePieceBitfield[pieceNumber>>3] & (byte)(Int32) 0x80 >> (Int32)(pieceNumber&0x7))!=0);
        }

        public void SetPieceOnRemotePeer(UInt32 pieceNumber)
        {
            _remotePieceBitfield[pieceNumber >> 3] |= (byte) ((Int32) 0x80 >> (Int32)(pieceNumber & 0x7));
        }

        public void PlaceBlockIntoPiece(UInt32 pieceNumber, UInt32 blockOffset)
        {
            try
            {
                UInt32 blockNumber = blockOffset / Constants.kBlockSize;

                Program.Logger.Trace($"placeBlockIntoPiece({pieceNumber},{blockOffset},{_packetLength - 9})");

                Buffer.BlockCopy(_readBuffer, 9, _assembledPiece.Buffer, (Int32)blockOffset, (Int32)_packetLength - 9);

                _torrentDownloader.Dc.BlockPieceDownloaded(pieceNumber, blockNumber, true);
                _torrentDownloader.Dc.BlockPieceRequested(pieceNumber, blockNumber, false);

                if (!_torrentDownloader.Dc.IsBlockPieceLast(pieceNumber, blockNumber))
                {
                    _torrentDownloader.Dc.totalBytesDownloaded += (UInt64)Constants.kBlockSize;
                }
                else
                {
                    _assembledPiece.Number = pieceNumber;
                    _torrentDownloader.Dc.totalBytesDownloaded += (UInt64)_torrentDownloader.Dc.pieceMap[pieceNumber].lastBlockLength;
                }
                if (TorrentDownloader.Dc.HasPieceBeenAssembled(pieceNumber))
                {
                    _waitForPieceAssembly.Set();
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

    }
}
