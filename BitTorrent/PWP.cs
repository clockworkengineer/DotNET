using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
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
            byte[] packedInt32 = new byte[4];
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

        private static void handleCHOKE(Peer remotePeer, byte[] messageBody)
        {
            remotePeer.PeerChoking = true;
            Console.WriteLine("CHOKE");
        }

        private static void handleUNCHOKE(Peer remotePeer, byte[] messageBody)
        {
            remotePeer.PeerChoking = false;
            Console.WriteLine("UNCHOKED");
        }

        private static void handleINTERESTED(Peer remotePeer, byte[] messageBody)
        {
            Console.WriteLine("INTERESTED");
        }

        private static void handleUNINTERESTED(Peer remotePeer, byte[] messageBody)
        {
            Console.WriteLine("UNINTERESTED");
        }

        private static void handleHAVE(Peer remotePeer, byte[] messageBody)
        {
            UInt32 pieceNumber = 0;

            pieceNumber = unpackUInt32(messageBody, 1);

            if (!remotePeer.FileDownloader.havePiece((int) pieceNumber)) { 
                PWP.interested(remotePeer);
                for (var blockNumber=0; blockNumber < remotePeer.FileDownloader.BlocksPerPiece; blockNumber++)
                {
                    remotePeer.FileDownloader.RemotePeerMap[pieceNumber].blocks[blockNumber] = true;
                }
            }

            Console.WriteLine($"Have piece= {pieceNumber}");
        }
       
        private static void handleBITMAP(Peer remotePeer, byte[] messageBody)
        {
            byte[] usageMap = new byte [messageBody.Length-1];
            List<bool> usage = new List<bool>();

            Buffer.BlockCopy(messageBody, 1, usageMap, 0, messageBody.Length-1);

            Console.WriteLine("\nUsage Map\n---------\n");
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
            Console.WriteLine(hex+"\n");
             
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
                        remotePeer.FileDownloader.RemotePeerMap[pieceNumber].blocks[blockNumber] = true;
                    }
                }
            }
        }

        private static void handleREQUEST(Peer remotePeer, byte[] messageBody)
        {
            Console.WriteLine("REQUEST");
        }

        private static void handlePIECE(Peer remotePeer, byte[] messageBody)
        {
            
            UInt32 pieceNumber = unpackUInt32(messageBody, 1);
            UInt32 blockOffset = unpackUInt32(messageBody, 5);

            Console.WriteLine($"Piece {pieceNumber} Block Offset {blockOffset} Data Size {messageBody.Length-9}\n");

            if (!remotePeer.FileDownloader.ReceivedMap[pieceNumber].blocks[blockOffset / Constants.kBlockSize]) {
                remotePeer.FileDownloader.TotalBytesDownloaded += (UInt64) Constants.kBlockSize;
                remotePeer.FileDownloader.ReceivedMap[pieceNumber].blocks[blockOffset / Constants.kBlockSize] = true;
            }

            Buffer.BlockCopy(messageBody, 9, remotePeer.FileDownloader.CurrentPiece, (int) blockOffset, messageBody.Length-9);

        }

        private static void handleCANCEL(Peer remotePeer, byte[] messageBody)
        {
            Console.WriteLine("CANCEL");
        }

        public static void remotePeerMessageProcess(Peer remotePeer)
        {
          
                byte[] messageBody = new byte[remotePeer.ReadBuffer.Length - 4];
                Buffer.BlockCopy(remotePeer.ReadBuffer, 4, messageBody, 0, messageBody.Length);
                switch (messageBody[0])
                {
                    case 0x0:
                        handleCHOKE(remotePeer, messageBody);
                        break;
                    case 0x1:
                        handleUNCHOKE(remotePeer, messageBody);
                        break;
                    case 0x2:
                        handleINTERESTED(remotePeer, messageBody);
                        break;
                    case 0x3:
                        handleUNINTERESTED(remotePeer, messageBody);
                        break;
                    case 0x4:
                        handleHAVE(remotePeer, messageBody);
                        break;
                    case 0x5:
                        handleBITMAP(remotePeer, messageBody);
                        break;
                    case 0x6:
                        handleREQUEST(remotePeer, messageBody);
                        break;
                    case 0x7:
                        handlePIECE(remotePeer, messageBody);
                        break;
                    case 0x8:
                        handleCANCEL(remotePeer, messageBody);
                        break;
                    default:
                        Console.WriteLine($"UNKOWN REQUEST{messageBody[0]}");
                        break;

               
            }

        }

        public static void choke(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(0);
    
            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

            remotePeer.AmChoking = true;
        }

        public static void unchoke(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(1);
    
            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

            remotePeer.AmChoking = false;

        }

        public static void interested(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(2);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void uninterested(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(3);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void have(Peer remotePeer, int pieceNumber)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(5));
            requestPacket.Add(4);
            requestPacket.AddRange(packUInt32(pieceNumber));

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void bitmap(Peer remotePeer, byte[] bitField)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(bitField.Length+1));
            requestPacket.Add(5);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void request(Peer remotePeer, int pieceNumber, int blockOffset, int blockSize)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(13));
            requestPacket.Add(6);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(packUInt32(blockSize));

            Console.WriteLine($"Request Piece {pieceNumber}  BlockOffset {blockOffset}");

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

         public static void piece(Peer remotePeer, int pieceNumber, int blockOffset, byte[] blockData)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(9+blockData.Length));
            requestPacket.Add(7);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(blockData);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);

        }

        public static void cancel(Peer remotePeer, int pieceNumber, int blockOffset, byte[] blockData)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(9 + blockData.Length));
            requestPacket.Add(8);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(blockData);

            peerWrite(remotePeer.PeerSocket,requestPacket.ToArray(), requestPacket.Count);


        }
    }
}
