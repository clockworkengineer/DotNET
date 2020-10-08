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
        /// Handles the choke.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleCHOKE(Peer remotePeer)
        {
            remotePeer.PeerChoking.Reset();
            Log.Logger.Debug("CHOKE");
        }

        /// <summary>
        /// Handles the unchoke.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleUNCHOKE(Peer remotePeer)
        {
            remotePeer.PeerChoking.Set();
            Log.Logger.Debug("UNCHOKED");
        }

        /// <summary>
        /// Handles the interested.
        /// </summary>
        /// <param name="remoePeer">Remoe peer.</param>
        private static void HandleINTERESTED(Peer remotePeer)
        {
            Log.Logger.Debug("INTERESTED");
        }

        /// <summary>
        /// Handles the uninterested.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleUNINTERESTED(Peer remotePeer)
        {
            Log.Logger.Debug("UNINTERESTED");
        }

        /// <summary>
        /// Handles the have.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleHAVE(Peer remotePeer)
        {
            uint pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);
            if (!remotePeer.Dc.IsPieceLocal(pieceNumber))
            {
                PWP.Interested(remotePeer);
                remotePeer.SetPieceOnRemotePeer(pieceNumber);
                remotePeer.Dc.MarkPieceOnPeer(pieceNumber, true);
            }

            Log.Logger.Debug($"Have piece= {pieceNumber}");
        }

        /// <summary>
        /// Handles the bitfield.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleBITFIELD(Peer remotePeer)
        {
            remotePeer.RemotePieceBitfield = new byte[(Int32)remotePeer.PacketLength - 1];

            Buffer.BlockCopy(remotePeer.ReadBuffer, 1, remotePeer.RemotePieceBitfield, 0, (Int32)remotePeer.PacketLength - 1);

            Log.Logger.Debug("\nUsage Map\n---------\n");
            StringBuilder hex = new StringBuilder(remotePeer.RemotePieceBitfield.Length);
            int byteCOunt = 0;
            foreach (byte b in remotePeer.RemotePieceBitfield)
            {
                hex.AppendFormat("{0:x2}", b);
                if (++byteCOunt % 16 == 0)
                {
                    hex.Append("\n");
                }
            }
            Log.Logger.Debug("\n" + hex + "\n");

            remotePeer.Dc.MergePieceBitfield(remotePeer);
        }

        /// <summary>
        /// Handles the request.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleREQUEST(Peer remotePeer)
        {
            Log.Logger.Debug("REQUEST");
        }

        /// <summary>
        /// Handles the piece.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandlePIECE(Peer remotePeer)
        {

            UInt32 pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);
            UInt32 blockOffset = UnpackUInt32(remotePeer.ReadBuffer, 5);

            Log.Logger.Debug($"Piece {pieceNumber} Block Offset {blockOffset} Data Size {(Int32)remotePeer.PacketLength - 9}\n");

            remotePeer.PlaceBlockIntoPiece(pieceNumber, blockOffset);

        }

        /// <summary>
        /// Handles the cancel.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        private static void HandleCANCEL(Peer remotePeer)
        {
            Log.Logger.Debug("CANCEL");
        }

        /// <summary>
        /// Perform initial handshake with remote peer that connected to local client to upload.
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

            return (connected, remotePeerID);
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
        /// Remotes the peer message process.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void RemotePeerMessageProcess(Peer remotePeer)
        {
            try
            {
                switch (remotePeer.ReadBuffer[0])
                {
                    case Constants.MessageCHOKE:
                        HandleCHOKE(remotePeer);
                        break;
                    case Constants.MessageUNCHOKE:
                        HandleUNCHOKE(remotePeer);
                        break;
                    case Constants.MessageINTERESTED:
                        HandleINTERESTED(remotePeer);
                        break;
                    case Constants.MessageUNINTERESTED:
                        HandleUNINTERESTED(remotePeer);
                        break;
                    case Constants.MessageHAVE:
                        HandleHAVE(remotePeer);
                        break;
                    case Constants.MessageBITFIELD:
                        HandleBITFIELD(remotePeer);
                        break;
                    case Constants.MessageREQUEST:
                        HandleREQUEST(remotePeer);
                        break;
                    case Constants.MessagePIECE:
                        HandlePIECE(remotePeer);
                        break;
                    case Constants.MessageCANCEL:
                        HandleCANCEL(remotePeer);
                        break;
                    default:
                        Log.Logger.Debug($"UNKOWN REQUEST{remotePeer.ReadBuffer[0]}");
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
        /// Choke the specified remotePeer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Choke(Peer remotePeer)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(Constants.MessageCHOKE);

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
        /// Unchoke the specified remotePeer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Unchoke(Peer remotePeer)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(Constants.MessageUNCHOKE);

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
        /// Interested the specified remotePeer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Interested(Peer remotePeer)
        {
            try
            {
                if (remotePeer.PeerInterested)
                {
                    List<byte> requestPacket = new List<byte>();

                    requestPacket.AddRange(PackUInt32(1));
                    requestPacket.Add(Constants.MessageINTERESTED);

                    remotePeer.PeerWrite(requestPacket.ToArray());

                    remotePeer.PeerInterested = true;
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
        /// Uninterested the specified remotePeer.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public static void Uninterested(Peer remotePeer)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(Constants.MessageUNINTERESTED);

                remotePeer.PeerWrite(requestPacket.ToArray());

                remotePeer.PeerInterested = false;
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
        /// Have the specified remotePeer and pieceNumber.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        public static void Have(Peer remotePeer, UInt32 pieceNumber)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(5));
                requestPacket.Add(Constants.MessageHAVE);
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
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)bitField.Length + 1));
                requestPacket.Add(Constants.MessageBITFIELD);

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
        /// Request the specified remotePeer, pieceNumber, blockOffset and blockSize.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        /// <param name="blockSize">Block size.</param>
        public static void Request(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, UInt32 blockSize)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(13));
                requestPacket.Add(Constants.MessageREQUEST);
                requestPacket.AddRange(PackUInt32(pieceNumber));
                requestPacket.AddRange(PackUInt32(blockOffset));
                requestPacket.AddRange(PackUInt32(blockSize));

                Log.Logger.Debug($"Request Piece {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockSize}");

                remotePeer.PeerWrite(requestPacket.ToArray());

                remotePeer.Dc.MarkPieceRequested(pieceNumber, true);
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
        /// Piece the specified remotePeer, pieceNumber, blockOffset and blockData.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        /// <param name="blockData">Block data.</param>
        public static void Piece(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, byte[] blockData)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)blockData.Length + 9));
                requestPacket.Add(Constants.MessagePIECE);
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
        /// Cancel the specified remotePeer, pieceNumber, blockOffset and blockData.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        /// <param name="blockData">Block data.</param>
        public static void Cancel(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, byte[] blockData)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)blockData.Length + 9));
                requestPacket.Add(Constants.MessageCANCEL);
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
    }
}
