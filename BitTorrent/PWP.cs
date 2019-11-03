using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace BitTorrent
{
    public class PWP
    {
        private static readonly byte[] _protocolName = Encoding.ASCII.GetBytes("BitTorrent protocol");

        private static void peerWrite(Socket peerSocket, byte[] buffer, int length)
        {

            peerSocket.Send(buffer);
             
        }

        private static int peerRead(Socket peerSocket, byte[] buffer, int length)
        {
 
            return (peerSocket.Receive(buffer, length, SocketFlags.None));
        }

        private static UInt32 unpackUInt32(byte[] buffer, int offset)
        {
            UInt32 unpackedUInt32 = 0;

            unpackedUInt32 = ((UInt32)buffer[offset + 0]) << 24;
            unpackedUInt32 |= ((UInt32)buffer[offset + 1]) << 16;
            unpackedUInt32 |= ((UInt32)buffer[offset + 2]) << 8;
            unpackedUInt32 |= ((UInt32)buffer[offset + 3]);

            return (unpackedUInt32);

        }

        private static byte[] packUInt32(int int32value)
        {
            byte[] packedInt32 = new byte[Constants.kMessageLength];
            packedInt32[0] = (byte)(int32value >> 24);
            packedInt32[1] = (byte)(int32value >> 16);
            packedInt32[2] = (byte)(int32value >> 8);
            packedInt32[3] = (byte)(int32value);

            return (packedInt32);

        }
        private static bool validatePeerConnect(byte[] handshakePacket, byte[] handshakeResponse, out byte[] remotePeerID)
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

        public static ValueTuple<bool, byte[]> intialHandshake(Peer remotePeer, byte[] infoHash)
        {
            List<byte> handshakePacket = new List<byte>();
            byte[] remotePeerID;

            handshakePacket.Add((byte) _protocolName.Length);
            handshakePacket.AddRange(_protocolName);
            handshakePacket.AddRange(new byte[8]);
            handshakePacket.AddRange(infoHash);
            handshakePacket.AddRange(Encoding.ASCII.GetBytes(PeerID.get()));

            peerWrite(remotePeer.PeerSocket, handshakePacket.ToArray(), handshakePacket.Count);

            byte[] handshakeResponse = new byte[handshakePacket.Count];

            peerRead(remotePeer.PeerSocket, handshakeResponse, handshakeResponse.Length);

            bool connected = validatePeerConnect(handshakePacket.ToArray(), handshakeResponse, out remotePeerID);

            return (connected, remotePeerID);

        }

        private static void handleCHOKE(Peer remotePeer)
        {
            remotePeer.PeerChoking = true;
            Program.Logger.Debug("CHOKE");
        }

        private static void handleUNCHOKE(Peer remotePeer)
        {
            remotePeer.PeerChoking = false;
            Program.Logger.Debug("UNCHOKED");
        }

        private static void handleINTERESTED(Peer remotePeer)
        {
            Program.Logger.Debug("INTERESTED");
        }

        private static void handleUNINTERESTED(Peer remotePeer)
        {
            Program.Logger.Debug("UNINTERESTED");
        }

        private static void handleHAVE(Peer remotePeer)
        {
            UInt32 pieceNumber = 0;

            pieceNumber = unpackUInt32(remotePeer.ReadBuffer, 1);

            if (!remotePeer.FileDownloader.havePiece((int) pieceNumber)) { 
                PWP.interested(remotePeer);
                for (var blockNumber=0; blockNumber < remotePeer.FileDownloader.BlocksPerPiece; blockNumber++)
                {
                    remotePeer.FileDownloader.RemotePeerMap[pieceNumber].blocks[blockNumber].mapped = true;
                }
            }

            Program.Logger.Debug($"Have piece= {pieceNumber}");
        }
       
        private static void handleBITFIELD(Peer remotePeer)
        {
            byte[] usageMap = new byte [remotePeer.ReadBuffer.Length-1];
            List<bool> usage = new List<bool>();

            Buffer.BlockCopy(remotePeer.ReadBuffer, 1, usageMap, 0, remotePeer.ReadBuffer.Length-1);

            Program.Logger.Debug("\nUsage Map\n---------\n");
            StringBuilder hex = new StringBuilder(usageMap.Length);
            int byteCOunt = 0;
            foreach (byte b in usageMap)
            {
                hex.AppendFormat("{0:x2}", b);
                if (++byteCOunt % 16 == 0)
                {
                    hex.Append("\n");
                }
            }
            Program.Logger.Debug(hex+"\n");
             
            for (int i=0; i < usageMap.Length; i++)
            {
                byte map = usageMap[i];
                usage.Add((map & 0x80)!=0);
                usage.Add((map & 0x40) != 0);
                usage.Add((map & 0x20) != 0);
                usage.Add((map & 0x10) != 0);
                usage.Add((map & 0x08) != 0);
                usage.Add((map & 0x04) != 0);
                usage.Add((map & 0x02) != 0);
                usage.Add((map & 0x01) != 0);
            }

            for (var pieceNumber=0; pieceNumber < remotePeer.FileDownloader.NumberOfPieces; pieceNumber++)
            {
                if (usage[pieceNumber])
                {
                    for (var blockNumber=0; blockNumber <remotePeer.FileDownloader.BlocksPerPiece; blockNumber++)
                    {
                        remotePeer.FileDownloader.RemotePeerMap[pieceNumber].blocks[blockNumber].mapped = true;
                    }
                }
            }
        }

        private static void handleREQUEST(Peer remotePeer)
        {
            Program.Logger.Debug("REQUEST");
        }

        private static void handlePIECE(Peer remotePeer)
        {
            
            UInt32 pieceNumber = unpackUInt32(remotePeer.ReadBuffer, 1);
            UInt32 blockOffset = unpackUInt32(remotePeer.ReadBuffer, 5);

            Program.Logger.Debug($"Piece {pieceNumber} Block Offset {blockOffset} Data Size {remotePeer.ReadBuffer.Length - 9}\n");

            if (!remotePeer.FileDownloader.ReceivedMap[pieceNumber].blocks[blockOffset / Constants.kBlockSize].mapped) {
                remotePeer.FileDownloader.TotalBytesDownloaded += (UInt64)remotePeer.FileDownloader.ReceivedMap[pieceNumber].blocks[blockOffset / Constants.kBlockSize].size;
                remotePeer.FileDownloader.ReceivedMap[pieceNumber].blocks[blockOffset / Constants.kBlockSize].mapped = true;
            }

            remotePeer.FileDownloader.placeBlockIntoPiece(remotePeer.ReadBuffer, 9,  (int) blockOffset, remotePeer.ReadBuffer.Length-9);

        }

        private static void handleCANCEL(Peer remotePeer)
        {
            Program.Logger.Debug("CANCEL");
        }

        public static void remotePeerMessageProcess(Peer remotePeer)
        {
          
 
                switch (remotePeer.ReadBuffer[0])
                {
                    case Constants.kMessageCHOKE:
                        handleCHOKE(remotePeer);
                        break;
                    case Constants.kMessageUNCHOKE:
                        handleUNCHOKE(remotePeer);
                        break;
                    case Constants.kMessageINTERESTED:
                        handleINTERESTED(remotePeer);
                        break;
                    case Constants.kMessageUNINTERESTED:
                        handleUNINTERESTED(remotePeer);
                        break;
                    case Constants.kMessageHAVE:
                        handleHAVE(remotePeer);
                        break;
                    case Constants.kMessageBITFIELD:
                        handleBITFIELD(remotePeer);
                        break;
                    case Constants.kMessageREQUEST:
                        handleREQUEST(remotePeer);
                        break;
                    case Constants.kMessagePIECE:
                        handlePIECE(remotePeer);
                        break;
                    case Constants.kMessageCANCEL:
                        handleCANCEL(remotePeer);
                        break;
                    default:
                        Program.Logger.Debug($"UNKOWN REQUEST{remotePeer.ReadBuffer[0]}");
                        break;

               
            }

        }

        public static void choke(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(Constants.kMessageCHOKE);
    
            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

            remotePeer.AmChoking = true;
        }

        public static void unchoke(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(Constants.kMessageUNCHOKE);
    
            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

            remotePeer.AmChoking = false;

        }

        public static void interested(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(Constants.kMessageINTERESTED);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void uninterested(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(Constants.kMessageUNINTERESTED);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void have(Peer remotePeer, int pieceNumber)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(5));
            requestPacket.Add(Constants.kMessageHAVE);
            requestPacket.AddRange(packUInt32(pieceNumber));

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void bitfield(Peer remotePeer, byte[] bitField)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(bitField.Length+1));
            requestPacket.Add(Constants.kMessageBITFIELD);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void request(Peer remotePeer, int pieceNumber, int blockOffset, int blockSize)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(13));
            requestPacket.Add(Constants.kMessageREQUEST);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(packUInt32(blockSize));

            Program.Logger.Debug($"Request Piece {pieceNumber}  BlockOffset {blockOffset} BlockSize {blockSize}");

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

         public static void piece(Peer remotePeer, int pieceNumber, int blockOffset, byte[] blockData)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(9+blockData.Length));
            requestPacket.Add(Constants.kMessagePIECE);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(blockData);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void cancel(Peer remotePeer, int pieceNumber, int blockOffset, byte[] blockData)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(9 + blockData.Length));
            requestPacket.Add(Constants.kMessageCANCEL);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(blockData);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);


        }
    }
}
