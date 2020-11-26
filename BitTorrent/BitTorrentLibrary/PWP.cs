//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Peer Wire Protocol code.
//
// Copyright 2020.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
namespace BitTorrentLibrary
{
    internal static class PWP
    {
        public delegate void PWPHandler(Peer remotePeer);
        private static readonly PWPHandler[] _protocolHandler =
        {
            HandleCHOKE,
            HandleUNCHOKE,
            HandleINTERESTED,
            HandleUNINTERESTED,
            HandleHAVE,
            HandleBITFIELD,
            HandleREQUEST,
            HandlePIECE,
            HandleCANCEL
        };
        /// <summary>
        ///  Ids of wire protocol commands
        /// </summary>
        private const byte CHOKE = 0;
        private const byte UNCHOKE = 1;
        private const byte INTERESTED = 2;
        private const byte UNINTERESTED = 3;
        private const byte HAVE = 4;
        private const byte BITFIELD = 5;
        private const byte REQUEST = 6;
        private const byte PIECE = 7;
        private const byte CANCEL = 8;
        private static readonly byte[] _protocolName = Encoding.ASCII.GetBytes("BitTorrent protocol");
        /// <summary>
        /// Convert remote peer ID to string
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <returns></returns>
        private static string RemotePeerID(Peer remotePeer)
        {
            return "[" + Encoding.ASCII.GetString(remotePeer.RemotePeerID) + "] ";
        }
        /// <summary>
        /// Extract protocol field from initial packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private static byte[] GetProtocol(byte[] packet)
        {
            byte[] protocol = new byte[19];
            Array.Copy(packet, 1, protocol, 0, protocol.Length);
            return protocol;
        }
        /// <summary>
        /// Extract infohash field from initial packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private static byte[] GetInfoHash(byte[] packet)
        {
            byte[] infoHash = new byte[20];
            Array.Copy(packet, 28, infoHash, 0, infoHash.Length);
            return infoHash;
        }
        /// <summary>
        /// Extract peerID field from initial packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private static byte[] GetClientID(byte[] packet)
        {
            byte[] clientID = new byte[20];
            Array.Copy(packet, 48, clientID, 0, clientID.Length);
            return clientID;
        }
        /// <summary>
        /// Dump out remote client connect packet information.
        /// </summary>
        /// <param name="packet"></param>
        private static void DumpRemoteClientInfo(byte[] packet)
        {
            Log.Logger.Debug($"Remote Client connect : Protocol [{Encoding.ASCII.GetString(GetProtocol(packet))}]" +
                             $"infoHash[{Util.InfoHashToString(GetInfoHash(packet))}] " +
                             $"ClientID [{Encoding.ASCII.GetString(GetClientID(packet))}]");
        }
        /// <summary>
        /// Dump bitfield to log.
        /// </summary>
        /// <param name="bitfield"></param>
        private static void DumpBitfield(byte[] bitfield)
        {
            Log.Logger.Info("Usage Map");
            StringBuilder hex = new StringBuilder(bitfield.Length);
            int byteCOunt = 0;
            foreach (byte b in bitfield)
            {
                hex.AppendFormat("{0:x2}", b);
                if (++byteCOunt % 16 == 0)
                {
                    hex.Append("\n");
                }
            }
            Log.Logger.Info("\n" + hex);
        }
        /// <summary>
        /// Create intial handshake to send to remote peer.
        /// </summary>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        private static List<byte> BuildInitialHandshake(byte[] infoHash)
        {
            List<byte> handshakePacket = new List<byte>
                {
                    (byte)_protocolName.Length
                };
            handshakePacket.AddRange(_protocolName);
            handshakePacket.AddRange(new byte[8]);
            handshakePacket.AddRange(infoHash);
            handshakePacket.AddRange(Encoding.ASCII.GetBytes(PeerID.Get()));
            return handshakePacket;
        }
        /// <summary>
        /// Validates the peer connect.
        /// </summary>
        /// <returns><c>true</c>, if peer connect was validated, <c>false</c> otherwise.</returns>
        /// <param name="localPacket">Handshake packet.</param>
        /// <param name="remotePacket">Handshake response.</param>
        /// <param name="remotePeerID">Remote peer identifier.</param>
        private static bool ValidatePeerConnect(byte[] localPacket, byte[] remotePacket, out byte[] remotePeerID)
        {
            remotePeerID = null;
            for (int byteNumber = 0; byteNumber < _protocolName.Length + 1; byteNumber++)
            {
                if (localPacket[byteNumber] != remotePacket[byteNumber])
                {
                    return false;
                }
            }
            for (int byteNumber = _protocolName.Length + 9; byteNumber < _protocolName.Length + 29; byteNumber++)
            {
                if (localPacket[byteNumber] != remotePacket[byteNumber])
                {
                    return false;
                }
            }
            remotePeerID = GetClientID(remotePacket);
            return true;
        }
        /// <summary>
        /// Handles choke command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleCHOKE(Peer remotePeer)
        {
            if (remotePeer.PeerChoking.WaitOne(0))
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}RX CHOKE");
                remotePeer.PeerChoking.Reset();
            }
        }
        /// <summary>
        /// Handles unchoke command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleUNCHOKE(Peer remotePeer)
        {
            if (!remotePeer.PeerChoking.WaitOne(0))
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}RX UNCHOKED");
                remotePeer.PeerChoking.Set();
            }
        }
        /// <summary>
        /// Handles interested command from remote peer.
        /// </summary>
        /// <param name="remoePeer">Remoe peer.</param>
        private static void HandleINTERESTED(Peer remotePeer)
        {
            if (!remotePeer.PeerInterested)
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}RX INTERESTED");
                remotePeer.PeerInterested = true;
            }
        }
        /// <summary>
        /// Handles uninterested command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleUNINTERESTED(Peer remotePeer)
        {
            if (remotePeer.PeerInterested)
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}RX UNINTERESTED");
                remotePeer.PeerInterested = false;
            }
        }
        /// <summary>
        /// Handles have piece command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleHAVE(Peer remotePeer)
        {
            uint pieceNumber = Util.UnPackUInt32(remotePeer.ReadBuffer, 1);
            Log.Logger.Info($"{RemotePeerID(remotePeer)}RX HAVE= {pieceNumber}");
            remotePeer.SetPieceOnRemotePeer(pieceNumber);
        }
        /// <summary>
        /// Handles bitfield command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleBITFIELD(Peer remotePeer)
        {
            Log.Logger.Info($"{RemotePeerID(remotePeer)}RX BITFIELD");
            Buffer.BlockCopy(remotePeer.ReadBuffer, 1, remotePeer.RemotePieceBitfield, 0, (Int32)remotePeer.PacketLength - 1);
            DumpBitfield(remotePeer.RemotePieceBitfield);
            remotePeer.Tc.MergePieceBitfield(remotePeer);
        }
        /// <summary>
        /// Handles request command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleREQUEST(Peer remotePeer)
        {
            PieceRequest request = new PieceRequest
            {
                infoHash = remotePeer.Tc.infoHash,
                ip = remotePeer.Ip,
                pieceNumber = Util.UnPackUInt32(remotePeer.ReadBuffer, 1),
                blockOffset = Util.UnPackUInt32(remotePeer.ReadBuffer, 5),
                blockSize = Util.UnPackUInt32(remotePeer.ReadBuffer, 9)
            };
            remotePeer.Tc.pieceRequestQueue.Add(request);
            Log.Logger.Info($"{RemotePeerID(remotePeer)}RX REQUEST {request.pieceNumber} Block Offset {request.blockOffset} Data Size {request.blockSize}.");
        }
        /// <summary>
        /// Handles piece command from a remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandlePIECE(Peer remotePeer)
        {
            UInt32 pieceNumber = Util.UnPackUInt32(remotePeer.ReadBuffer, 1);
            UInt32 blockOffset = Util.UnPackUInt32(remotePeer.ReadBuffer, 5);
            Log.Logger.Info($"{RemotePeerID(remotePeer)}RX PIECE {pieceNumber} Block Offset {blockOffset} Data Size {(Int32)remotePeer.PacketLength - 9}");
            remotePeer.PlaceBlockIntoPiece(pieceNumber, blockOffset);
            if (remotePeer.packetResponseTimer.IsRunning)
            {
                remotePeer.packetResponseTimer.Stop();
                remotePeer.averagePacketResponse.Add(remotePeer.packetResponseTimer.ElapsedMilliseconds);
            }
        }
        /// <summary>
        /// Handles cancel command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleCANCEL(Peer remotePeer)
        {
            UInt32 pieceNumber = Util.UnPackUInt32(remotePeer.ReadBuffer, 1);
            UInt32 blockOffset = Util.UnPackUInt32(remotePeer.ReadBuffer, 5);
            UInt32 blockLength = Util.UnPackUInt32(remotePeer.ReadBuffer, 9);
            Log.Logger.Info($"{RemotePeerID(remotePeer)}RX CANCEL {pieceNumber} Block Offset {blockOffset} Data Size {blockLength}.");
        }
        /// <summary>
        /// Perform initial handshake with remote peer that we are connected. If we are been connected too remotely
        /// we must check the connecting peers info hash is valid and hookup the peers torrent context if it is.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="manager"></param>
        /// <returns>Tuple<bbol, byte[]> indicating connection succucess and the ID of the remote client.</returns>
        public static ValueTuple<bool, byte[]> Handshake(Peer remotePeer, Manager manager)
        {
            List<byte> localPacket=null; 
            if (remotePeer.Tc != null)
            {
                localPacket = BuildInitialHandshake(remotePeer.Tc.infoHash);
                remotePeer.Write(localPacket.ToArray());
            }
            byte[] remotePacket = new byte[Constants.IntialHandshakeLength];
            Int32 bytesRead = remotePeer.Read(remotePacket, remotePacket.Length);
            if (bytesRead != remotePacket.Length)
            {
                throw new Exception("Invalid length read for intial packet exchange.");
            }
            DumpRemoteClientInfo(remotePacket);
            bool connected = false;
            byte[] remotePeerID = new byte[Constants.PeerIDLength];
            if (remotePeer.Tc is null)
            {
                foreach (var tc in manager.TorrentList)
                {
                    localPacket = BuildInitialHandshake(tc.infoHash);
                    connected = ValidatePeerConnect(localPacket.ToArray(), remotePacket, out remotePeerID);
                    if (connected)
                    {
                        remotePeer.Write(localPacket.ToArray());
                        remotePeer.SetTorrentContext(tc);
                        break;
                    }
                }
                if (!connected)
                {
                    throw new Exception($"Remote peer [{remotePeer.Ip}] has the incorrect infohash.");
                }
            }
            else
            {
                connected = ValidatePeerConnect(localPacket.ToArray(), remotePacket, out remotePeerID);
            }
            return (connected, remotePeerID);
        }
        /// <summary>
        /// Route the peer message to process.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void MessageProcess(Peer remotePeer)
        {
            if (remotePeer.Connected)
            {
                byte command = remotePeer.ReadBuffer[0];
                if ((command >= CHOKE) && (command <= CANCEL))
                {
                    _protocolHandler[remotePeer.ReadBuffer[0]](remotePeer);
                }
                else
                {
                    Log.Logger.Info($"{RemotePeerID(remotePeer)}RX UNKOWN REQUEST{command}");
                }
            }
        }
        /// <summary>
        /// Choke the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Choke(Peer remotePeer)
        {
            if (remotePeer.Connected)
            {
                if (!remotePeer.AmChoking)
                {
                    Log.Logger.Info($"{RemotePeerID(remotePeer)}TX CHOKE");
                    List<byte> requestPacket = new List<byte>();
                    requestPacket.AddRange(Util.PackUInt32(1));
                    requestPacket.Add(CHOKE);
                    remotePeer.Write(requestPacket.ToArray());
                    remotePeer.AmChoking = true;
                }
            }
        }
        /// <summary>
        /// Unchoke the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Unchoke(Peer remotePeer)
        {
            if (remotePeer.Connected)
            {
                if (remotePeer.AmChoking)
                {
                    Log.Logger.Info($"{RemotePeerID(remotePeer)}TX UNCHOKE");
                    List<byte> requestPacket = new List<byte>();
                    requestPacket.AddRange(Util.PackUInt32(1));
                    requestPacket.Add(UNCHOKE);
                    remotePeer.Write(requestPacket.ToArray());
                    remotePeer.AmChoking = false;
                }
            }
        }
        /// <summary>
        /// Signal interest the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Interested(Peer remotePeer)
        {
            if (remotePeer.Connected)
            {
                if (!remotePeer.AmInterested)
                {
                    Log.Logger.Info($"{RemotePeerID(remotePeer)}TX INTERESTED");
                    List<byte> requestPacket = new List<byte>();
                    requestPacket.AddRange(Util.PackUInt32(1));
                    requestPacket.Add(INTERESTED);
                    remotePeer.Write(requestPacket.ToArray());
                    remotePeer.AmInterested = true;
                }
            }
        }
        /// <summary>
        /// Signal uninterest the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Uninterested(Peer remotePeer)
        {
            if (remotePeer.Connected)
            {
                if (remotePeer.AmInterested)
                {
                    Log.Logger.Info($"{RemotePeerID(remotePeer)}TX UNINTERESTED");
                    List<byte> requestPacket = new List<byte>();
                    requestPacket.AddRange(Util.PackUInt32(1));
                    requestPacket.Add(UNINTERESTED);
                    remotePeer.Write(requestPacket.ToArray());
                    remotePeer.AmInterested = false;
                }
            }
        }
        /// <summary>
        /// Signal that have the piece to specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        public static void Have(Peer remotePeer, UInt32 pieceNumber)
        {
            if (remotePeer.Connected)
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}TX HAVE {pieceNumber}");
                List<byte> requestPacket = new List<byte>();
                requestPacket.AddRange(Util.PackUInt32(5));
                requestPacket.Add(HAVE);
                requestPacket.AddRange(Util.PackUInt32(pieceNumber));
                remotePeer.Write(requestPacket.ToArray());
            }
        }
        /// <summary>
        /// Send current piece bitfield to remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="bitField">Bit field.</param>
        public static void Bitfield(Peer remotePeer, byte[] bitField)
        {
            if (remotePeer.Connected)
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}TX BITFIELD");
                DumpBitfield(bitField);
                List<byte> requestPacket = new List<byte>();
                requestPacket.AddRange(Util.PackUInt32((UInt32)bitField.Length + 1));
                requestPacket.Add(BITFIELD);
                requestPacket.AddRange(bitField);
                remotePeer.Write(requestPacket.ToArray());
            }
        }
        /// <summary>
        /// Request the specified piece number, block offset and block size from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        /// <param name="blockSize">Block size.</param>
        public static void Request(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, UInt32 blockSize)
        {
            if (remotePeer.Connected)
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}TX REQUEST Piece {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockSize}");
                List<byte> requestPacket = new List<byte>();
                requestPacket.AddRange(Util.PackUInt32(13));
                requestPacket.Add(REQUEST);
                requestPacket.AddRange(Util.PackUInt32(pieceNumber));
                requestPacket.AddRange(Util.PackUInt32(blockOffset));
                requestPacket.AddRange(Util.PackUInt32(blockSize));
                remotePeer.Write(requestPacket.ToArray());
                if (!remotePeer.packetResponseTimer.IsRunning)
                {
                    remotePeer.packetResponseTimer.Start();
                }
            }
        }
        /// <summary>
        /// Return specified piece, block offset plus its data to remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        /// <param name="blockData">Block data.</param>
        public static void Piece(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, byte[] blockData)
        {
            if (remotePeer.Connected)
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}TX PIECE {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockData.Length}");
                List<byte> requestPacket = new List<byte>();
                requestPacket.AddRange(Util.PackUInt32((UInt32)blockData.Length + 9));
                requestPacket.Add(PIECE);
                requestPacket.AddRange(Util.PackUInt32(pieceNumber));
                requestPacket.AddRange(Util.PackUInt32(blockOffset));
                requestPacket.AddRange(blockData);
                remotePeer.Write(requestPacket.ToArray());
            }
        }
        /// <summary>
        /// Cancel the specified piece, block offset and block size request to remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        /// <param name="blockData">Block data.</param>
        public static void Cancel(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, UInt32 blockSize)
        {
            if (remotePeer.Connected)
            {
                Log.Logger.Info($"{RemotePeerID(remotePeer)}TX CANCEL Piece {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockSize}");
                List<byte> requestPacket = new List<byte>();
                requestPacket.AddRange(Util.PackUInt32(13));
                requestPacket.Add(CANCEL);
                requestPacket.AddRange(Util.PackUInt32(pieceNumber));
                requestPacket.AddRange(Util.PackUInt32(blockOffset));
                requestPacket.AddRange(Util.PackUInt32(blockSize));
            }
        }
    }
}
