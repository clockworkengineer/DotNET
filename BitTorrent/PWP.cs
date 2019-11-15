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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace BitTorrent
{
    public class PWP
    {
        private static readonly byte[] _protocolName = Encoding.ASCII.GetBytes("BitTorrent protocol");

        private static void PeerWrite(Socket peerSocket, byte[] buffer, int length)
        {
            peerSocket.Send(buffer);
        }

        private static int PeerRead(Socket peerSocket, byte[] buffer, int length)
        {
            return (peerSocket.Receive(buffer, length, SocketFlags.None));
        }

        private static UInt32 UnpackUInt32(byte[] buffer, int offset)
        {
            UInt32 unpackedUInt32 = 0;

            unpackedUInt32 = ((UInt32)buffer[offset + 0]) << 24;
            unpackedUInt32 |= ((UInt32)buffer[offset + 1]) << 16;
            unpackedUInt32 |= ((UInt32)buffer[offset + 2]) << 8;
            unpackedUInt32 |= ((UInt32)buffer[offset + 3]);

            return (unpackedUInt32);

        }

        private static byte[] PackUInt32(UInt32 int32value)
        {
            byte[] packedInt32 = new byte[Constants.kSizeOfUInt32];
            packedInt32[0] = (byte)(int32value >> 24);
            packedInt32[1] = (byte)(int32value >> 16);
            packedInt32[2] = (byte)(int32value >> 8);
            packedInt32[3] = (byte)(int32value);

            return (packedInt32);

        }

        private static bool ValidatePeerConnect(byte[] handshakePacket, byte[] handshakeResponse, out byte[] remotePeerID)
        {

            remotePeerID = null;

            for (int byteNumber = 0; byteNumber < _protocolName.Length + 1; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return (false);
                }
            }
            for (int byteNumber = _protocolName.Length + 9; byteNumber < _protocolName.Length + 29; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return (false);
                }
            }

            remotePeerID = new byte[Constants.kHashLength];

            Buffer.BlockCopy(handshakeResponse, _protocolName.Length + 29, remotePeerID, 0, remotePeerID.Length);

            return (true);

        }

        private static void HandleCHOKE(Peer remotePeer)
        {
            remotePeer.PeerChoking = true;
            Program.Logger.Debug("CHOKE");
        }

        private static void HandleUNCHOKE(Peer remotePeer)
        {
            remotePeer.PeerChoking = false;
            Program.Logger.Debug("UNCHOKED");
        }

        private static void HandleINTERESTED(Peer remotePeer)
        {
            Program.Logger.Debug("INTERESTED");
        }

        private static void HandleUNINTERESTED(Peer remotePeer)
        {
            Program.Logger.Debug("UNINTERESTED");
        }

        private static void HandleHAVE(Peer remotePeer)
        {
            UInt32 pieceNumber = 0;

            pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);

            if (!remotePeer.TorrentDownloader.HavePiece(pieceNumber))
            {
                PWP.Interested(remotePeer);
                remotePeer.SetPieceOnRemotePeer(pieceNumber);
                for (UInt32 blockNumber = 0; blockNumber < remotePeer.TorrentDownloader.Dc.blocksPerPiece; blockNumber++)
                {
                    remotePeer.TorrentDownloader.Dc.BlockPieceOnPeer(pieceNumber, blockNumber, true);
                }
            }

            Program.Logger.Debug($"Have piece= {pieceNumber}");
        }

        private static void HandleBITFIELD(Peer remotePeer)
        {

            remotePeer.RemotePieceBitfield = new byte[(Int32)remotePeer.PacketLength - 1];

            Buffer.BlockCopy(remotePeer.ReadBuffer, 1, remotePeer.RemotePieceBitfield, 0, (Int32) remotePeer.PacketLength - 1);

            Program.Logger.Debug("\nUsage Map\n---------\n");
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
            Program.Logger.Debug(hex + "\n");

            remotePeer.TorrentDownloader.Dc.MergePieceBitfield(remotePeer);

        }

        private static void HandleREQUEST(Peer remotePeer)
        {
            Program.Logger.Debug("REQUEST");
        }

        private static void HandlePIECE(Peer remotePeer)
        {

            UInt32 pieceNumber = UnpackUInt32(remotePeer.ReadBuffer, 1);
            UInt32 blockOffset = UnpackUInt32(remotePeer.ReadBuffer, 5);

            Program.Logger.Debug($"Piece {pieceNumber} Block Offset {blockOffset} Data Size {(Int32)remotePeer.PacketLength - 9}\n");

            remotePeer.TorrentDownloader.PlaceBlockIntoPiece(remotePeer.ReadBuffer, pieceNumber, blockOffset, (UInt32)remotePeer.PacketLength - 9);

        }

        private static void HandleCANCEL(Peer remotePeer)
        {
            Program.Logger.Debug("CANCEL");
        }

        public static ValueTuple<bool, byte[]> intialHandshake(Peer remotePeer, byte[] infoHash)
        {

            byte[] remotePeerID = null;
            bool connected = false;

            try
            {
                List<byte> handshakePacket = new List<byte>();

                handshakePacket.Add((byte)_protocolName.Length);
                handshakePacket.AddRange(_protocolName);
                handshakePacket.AddRange(new byte[8]);
                handshakePacket.AddRange(infoHash);
                handshakePacket.AddRange(Encoding.ASCII.GetBytes(PeerID.get()));

                PeerWrite(remotePeer.PeerSocket, handshakePacket.ToArray(), handshakePacket.Count);

                byte[] handshakeResponse = new byte[handshakePacket.Count];

                PeerRead(remotePeer.PeerSocket, handshakeResponse, handshakeResponse.Length);

                connected = ValidatePeerConnect(handshakePacket.ToArray(), handshakeResponse, out remotePeerID);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

            return (connected, remotePeerID);

        }

  
        public static void RemotePeerMessageProcess(Peer remotePeer)
        {

            try
            {
                switch (remotePeer.ReadBuffer[0])
                {
                    case Constants.kMessageCHOKE:
                        HandleCHOKE(remotePeer);
                        break;
                    case Constants.kMessageUNCHOKE:
                        HandleUNCHOKE(remotePeer);
                        break;
                    case Constants.kMessageINTERESTED:
                        HandleINTERESTED(remotePeer);
                        break;
                    case Constants.kMessageUNINTERESTED:
                        HandleUNINTERESTED(remotePeer);
                        break;
                    case Constants.kMessageHAVE:
                        HandleHAVE(remotePeer);
                        break;
                    case Constants.kMessageBITFIELD:
                        HandleBITFIELD(remotePeer);
                        break;
                    case Constants.kMessageREQUEST:
                        HandleREQUEST(remotePeer);
                        break;
                    case Constants.kMessagePIECE:
                        HandlePIECE(remotePeer);
                        break;
                    case Constants.kMessageCANCEL:
                        HandleCANCEL(remotePeer);
                        break;
                    default:
                        Program.Logger.Debug($"UNKOWN REQUEST{remotePeer.ReadBuffer[0]}");
                        break;

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

        public static void Choke(Peer remotePeer)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(Constants.kMessageCHOKE);

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);
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

        public static void Unchoke(Peer remotePeer)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(Constants.kMessageUNCHOKE);

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);
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

        public static void Interested(Peer remotePeer)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(Constants.kMessageINTERESTED);

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);

                remotePeer.Interested = true;
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

        public static void Uninterested(Peer remotePeer)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(1));
                requestPacket.Add(Constants.kMessageUNINTERESTED);

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);

                remotePeer.Interested = false;
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

        public static void Have(Peer remotePeer, UInt32 pieceNumber)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(5));
                requestPacket.Add(Constants.kMessageHAVE);
                requestPacket.AddRange(PackUInt32(pieceNumber));

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);
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

        public static void Bitfield(Peer remotePeer, byte[] bitField)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)bitField.Length + 1));
                requestPacket.Add(Constants.kMessageBITFIELD);

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);
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

        public static void Request(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, UInt32 blockSize)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32(13));
                requestPacket.Add(Constants.kMessageREQUEST);
                requestPacket.AddRange(PackUInt32(pieceNumber));
                requestPacket.AddRange(PackUInt32(blockOffset));
                requestPacket.AddRange(PackUInt32(blockSize));

                Program.Logger.Debug($"Request Piece {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockSize}");

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);
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

        public static void Piece(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, byte[] blockData)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)blockData.Length + 9));
                requestPacket.Add(Constants.kMessagePIECE);
                requestPacket.AddRange(PackUInt32(pieceNumber));
                requestPacket.AddRange(PackUInt32(blockOffset));
                requestPacket.AddRange(blockData);

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);
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

        public static void Cancel(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, byte[] blockData)
        {
            try
            {
                List<byte> requestPacket = new List<byte>();

                requestPacket.AddRange(PackUInt32((UInt32)blockData.Length + 9));
                requestPacket.Add(Constants.kMessageCANCEL);
                requestPacket.AddRange(PackUInt32(pieceNumber));
                requestPacket.AddRange(PackUInt32(blockOffset));
                requestPacket.AddRange(blockData);

                PeerWrite(remotePeer.PeerSocket, requestPacket.ToArray(), requestPacket.Count);
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
