//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Peer Wire Protocol handling code.
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace BitTorrentLibrary
{
    public static class PWP
    {

        public const byte CHOKE = 0;        // Ids of wire protocol messages
        public const byte UNCHOKE = 1;
        public const byte INTERESTED = 2;
        public const byte UNINTERESTED = 3;
        public const byte HAVE = 4;
        public const byte BITFIELD = 5;
        public const byte REQUEST = 6;
        public const byte PIECE = 7;
        public const byte CANCEL = 8;

        private static readonly byte[] _protocolName = Encoding.ASCII.GetBytes("BitTorrent protocol");

        /// <summary>
        /// Unpacks a uint32 from a byte buffer at a given offset.
        /// </summary>
        /// <returns>The user interface nt32.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="offset">Offset.</param>
        private static UInt32 UnpackUInt32(byte[] buffer, int offset)
        {
            UInt32 unpackedUInt32 = ((UInt32)buffer[offset + 0]) << 24;
            unpackedUInt32 |= ((UInt32)buffer[offset + 1]) << 16;
            unpackedUInt32 |= ((UInt32)buffer[offset + 2]) << 8;
            unpackedUInt32 |= buffer[offset + 3];

            return unpackedUInt32;
        }

        /// <summary>
        /// Packs a UInt32 into a byte array.
        /// </summary>
        /// <returns>Packed byte array.</returns>
        /// <param name="uInt32value">Int32value.</param>
        private static byte[] PackUInt32(UInt32 uInt32value)
        {
            byte[] packedUInt32 = new byte[Constants.SizeOfUInt32];
            packedUInt32[0] = (byte)(uInt32value >> 24);
            packedUInt32[1] = (byte)(uInt32value >> 16);
            packedUInt32[2] = (byte)(uInt32value >> 8);
            packedUInt32[3] = (byte)(uInt32value);

            return packedUInt32;
        }
        /// <summary>
        /// Dump bitfield to log.
        /// </summary>
        /// <param name="bitfield"></param>
        private static void DumpBitfield(byte[] bitfield)
        {
            Log.Logger.Info("\nUsage Map\n---------\n");
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
            Log.Logger.Info("\n" + hex + "\n");
        }
        /// <summary>
        /// Create intial handshake to send to remote peer.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        private static List<byte> BuildInitialHandshake(Peer remotePeer, byte[] infoHash)
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
        /// <param name="handshakePacket">Handshake packet.</param>
        /// <param name="handshakeResponse">Handshake response.</param>
        /// <param name="remotePeerID">Remote peer identifier.</param>
        private static bool ValidatePeerConnect(byte[] handshakePacket, byte[] handshakeResponse, out byte[] remotePeerID)
        {
            remotePeerID = null;

            for (int byteNumber = 0; byteNumber < _protocolName.Length + 1; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return false;
                }
            }
            for (int byteNumber = _protocolName.Length + 9; byteNumber < _protocolName.Length + 29; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return false;
                }
            }

            remotePeerID = new byte[Constants.HashLength];

            Buffer.BlockCopy(handshakeResponse, _protocolName.Length + 29, remotePeerID, 0, remotePeerID.Length);

            return true;
        }

        /// <summary>
        /// Handles choke command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleCHOKE(Peer remotePeer)
        {
            Log.Logger.Info("RX CHOKE");
            remotePeer.PeerChoking.Reset();
            remotePeer.WaitForPieceAssembly.Set();
        }

        /// <summary>
        /// Handles unchoke command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleUNCHOKE(Peer remotePeer)
        {
            Log.Logger.Info("RX UNCHOKED");
            remotePeer.PeerChoking.Set();
        }

        /// <summary>
        /// Handles interested command from remote peer.
        /// </summary>
        /// <param name="remoePeer">Remoe peer.</param>
        private static void HandleINTERESTED(Peer remotePeer)
        {
            Log.Logger.Info("RX INTERESTED");
            remotePeer.PeerInterested = true;
        }

        /// <summary>
        /// Handles uninterested command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleUNINTERESTED(Peer remotePeer)
        {
            Log.Logger.Info("RX UNINTERESTED");
            remotePeer.PeerInterested = false;
        }

        /// <summary>
        /// Handles have piece command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleHAVE(Peer remotePeer)
        {
            uint pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);

            Log.Logger.Info($"RX HAVE= {pieceNumber}");

            remotePeer.SetPieceOnRemotePeer(pieceNumber);
            remotePeer.Dc.MarkPieceOnPeer(pieceNumber, true);

            if (!remotePeer.Dc.IsPieceLocal(pieceNumber))
            {
                PWP.Interested(remotePeer);
            }
        }

        /// <summary>
        /// Handles bitfield command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleBITFIELD(Peer remotePeer)
        {
            Log.Logger.Info("RX BITFIELD");

            remotePeer.RemotePieceBitfield = new byte[(Int32)remotePeer.PacketLength - 1];

            Buffer.BlockCopy(remotePeer.ReadBuffer, 1, remotePeer.RemotePieceBitfield, 0, (Int32)remotePeer.PacketLength - 1);

            DumpBitfield(remotePeer.RemotePieceBitfield);

            remotePeer.Dc.MergePieceBitfield(remotePeer);
        }

        /// <summary>
        /// Handles request command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleREQUEST(Peer remotePeer)
        {


            PieceRequest request = new PieceRequest();

            request.remotePeer = remotePeer;
            request.pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);
            request.blockOffset = UnpackUInt32(remotePeer.ReadBuffer, 5);
            request.blockSize = UnpackUInt32(remotePeer.ReadBuffer, 9);

            remotePeer.Dc.PieceRequestQueue.Add(request);

            Log.Logger.Info($"RX REQUEST {request.pieceNumber} Block Offset {request.blockOffset} Data Size {request.blockSize}\n.");


        }

        /// <summary>
        /// Handles piece command from a remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandlePIECE(Peer remotePeer)
        {

            UInt32 pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);
            UInt32 blockOffset = UnpackUInt32(remotePeer.ReadBuffer, 5);

            Log.Logger.Info($"RX PIECE {pieceNumber} Block Offset {blockOffset} Data Size {(Int32)remotePeer.PacketLength - 9}\n");

            remotePeer.PlaceBlockIntoPiece(pieceNumber, blockOffset);

        }

        /// <summary>
        /// Handles cacnel command from remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleCANCEL(Peer remotePeer)
        {
            UInt32 pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);
            UInt32 blockOffset = UnpackUInt32(remotePeer.ReadBuffer, 5);
            UInt32 blockLength = UnpackUInt32(remotePeer.ReadBuffer, 9);

            Log.Logger.Info($"RX CANCEL {pieceNumber} Block Offset {blockOffset} Data Size {blockLength}\n.");
        }

        /// <summary>
        /// Perform initial handshake with remote peer that connected to local client.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="infoHash"></param>
        /// <returns>Tuple<bbol, byte[]> indicating connection cucess and the ID of the remote client.</returns>
        public static ValueTuple<bool, byte[]> ConnectFromIntialHandshake(Peer remotePeer, byte[] infoHash)
        {
            byte[] remotePeerID = null;
            bool connected = false;

            try
            {
                List<byte> handshakePacket = BuildInitialHandshake(remotePeer, infoHash);

                byte[] handshakeResponse = new byte[handshakePacket.Count];

                UInt32 bytesRead = (UInt32)remotePeer.PeerRead(handshakeResponse, handshakeResponse.Length);

                connected = ValidatePeerConnect(handshakeResponse, handshakePacket.ToArray(), out remotePeerID);

                if (connected)
                {
                    remotePeer.PeerWrite(handshakePacket.ToArray());
                }

                return (connected, remotePeerID);

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }

        }

        /// <summary>
        /// Perform initial handshake with remote peer that the local client connected to.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="infoHash"></param>
        /// <returns>Tuple<bbol, byte[]> indicating connection cucess and the ID of the remote client.</returns>
        public static ValueTuple<bool, byte[]> ConnectToIntialHandshake(Peer remotePeer, byte[] infoHash)
        {
            byte[] remotePeerID = null;
            bool connected = false;

            try
            {
                List<byte> handshakePacket = BuildInitialHandshake(remotePeer, infoHash);

                remotePeer.PeerWrite(handshakePacket.ToArray());

                byte[] handshakeResponse = new byte[handshakePacket.Count];

                remotePeer.PeerRead(handshakeResponse, handshakeResponse.Length);

                connected = ValidatePeerConnect(handshakePacket.ToArray(), handshakeResponse, out remotePeerID);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }

            return (connected, remotePeerID);
        }

        /// <summary>
        /// Route the peer message to process.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void RemotePeerMessageProcess(Peer remotePeer)
        {
            try
            {
                switch (remotePeer.ReadBuffer[0])
                {
                    case CHOKE:
                        HandleCHOKE(remotePeer);
                        break;
                    case UNCHOKE:
                        HandleUNCHOKE(remotePeer);
                        break;
                    case INTERESTED:
                        HandleINTERESTED(remotePeer);
                        break;
                    case UNINTERESTED:
                        HandleUNINTERESTED(remotePeer);
                        break;
                    case HAVE:
                        HandleHAVE(remotePeer);
                        break;
                    case BITFIELD:
                        HandleBITFIELD(remotePeer);
                        break;
                    case REQUEST:
                        HandleREQUEST(remotePeer);
                        break;
                    case PIECE:
                        HandlePIECE(remotePeer);
                        break;
                    case CANCEL:
                        HandleCANCEL(remotePeer);
                        break;
                    default:
                        Log.Logger.Info($"RX UNKOWN REQUEST{remotePeer.ReadBuffer[0]}");
                        break;
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Choke the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Choke(Peer remotePeer)
        {
            try
            {
                if (!remotePeer.AmChoking)
                {
                    Log.Logger.Info("TX CHOKE");

                    List<byte> requestPacket = new List<byte>();

                    requestPacket.AddRange(PackUInt32(1));
                    requestPacket.Add(CHOKE);

                    remotePeer.PeerWrite(requestPacket.ToArray());

                    remotePeer.AmChoking = true;
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Unchoke the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Unchoke(Peer remotePeer)
        {
            try
            {
                if (remotePeer.AmChoking)
                {
                    Log.Logger.Info("TX UNCHOKE");

                    List<byte> requestPacket = new List<byte>();

                    requestPacket.AddRange(PackUInt32(1));
                    requestPacket.Add(UNCHOKE);

                    remotePeer.PeerWrite(requestPacket.ToArray());

                    remotePeer.AmChoking = false;
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Signal interest the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Interested(Peer remotePeer)
        {
            try
            {
                if (!remotePeer.AmInterested)
                {
                    Log.Logger.Info("TX INTERESTED");

                    List<byte> requestPacket = new List<byte>();

                    requestPacket.AddRange(PackUInt32(1));
                    requestPacket.Add(INTERESTED);

                    remotePeer.PeerWrite(requestPacket.ToArray());

                    remotePeer.AmInterested = true;
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Signa uninterest the specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Uninterested(Peer remotePeer)
        {
            try
            {
                Log.Logger.Info("TX UNINTERESTED");

                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(UNINTERESTED);

                remotePeer.PeerWrite(requestPacket.ToArray());

                remotePeer.AmInterested = false;
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Signal that have the piece to specified remote peer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        public static void Have(Peer remotePeer, UInt32 pieceNumber)
        {
            try
            {
                Log.Logger.Info($"TX HAVE {pieceNumber}");

                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(5));
                requestPacket.Add(HAVE);
                requestPacket.AddRange(PackUInt32(pieceNumber));

                remotePeer.PeerWrite(requestPacket.ToArray());
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Bitfield the specified remotePeer and bitField.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="bitField">Bit field.</param>
        public static void Bitfield(Peer remotePeer, byte[] bitField)
        {
            try
            {
                Log.Logger.Info("TX BITFIELD");

                DumpBitfield(bitField);

                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)bitField.Length + 1));
                requestPacket.Add(BITFIELD);

                remotePeer.PeerWrite(requestPacket.ToArray());
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
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
            try
            {
                Log.Logger.Info($"TX REQUEST Piece {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockSize}");

                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(13));
                requestPacket.Add(REQUEST);
                requestPacket.AddRange(PackUInt32(pieceNumber));
                requestPacket.AddRange(PackUInt32(blockOffset));
                requestPacket.AddRange(PackUInt32(blockSize));

                remotePeer.PeerWrite(requestPacket.ToArray());

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
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
            try
            {
                Log.Logger.Info($"TX PIECE {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockData.Length}");

                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)blockData.Length + 9));
                requestPacket.Add(PIECE);
                requestPacket.AddRange(PackUInt32(pieceNumber));
                requestPacket.AddRange(PackUInt32(blockOffset));
                requestPacket.AddRange(blockData);

                remotePeer.PeerWrite(requestPacket.ToArray());
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
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
            try
            {
                Log.Logger.Info($"TX CANCEL Piece {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockSize}");

                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(13));
                requestPacket.Add(CANCEL);
                requestPacket.AddRange(PackUInt32(pieceNumber));
                requestPacket.AddRange(PackUInt32(blockOffset));
                requestPacket.AddRange(PackUInt32(blockSize));

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (PWP) Error: " + ex.Message);
            }
        }
    }
}
