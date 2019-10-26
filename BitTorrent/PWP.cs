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

            remotePeerID = new byte[20];

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

            remotePeer.PeerStream.Write(handshakePacket.ToArray(), 0, handshakePacket.Count);

            byte[] handshakeResponse = new byte[handshakePacket.Count];

            remotePeer.PeerStream.Read(handshakeResponse, 0, handshakeResponse.Length);

            bool connected = validatePeerConnect(handshakePacket.ToArray(), handshakeResponse, out remotePeerID);

            return (connected, remotePeerID);

        }

        private static void handleCHOKE(Peer remotePeer, byte[] messageBody)
        {
            remotePeer.PeerChoking = true;
        }

        private static void handleUNCHOKE(Peer remotePeer, byte[] messageBody)
        {
            remotePeer.PeerChoking = false;
        }

        private static void handleINTERESTED(Peer remotePeer, byte[] messageBody)
        {

        }

        private static void handleUNINTERESTED(Peer remotePeer, byte[] messageBody)
        {

        }

        private static void handleHAVE(Peer remotePeer, byte[] messageBody)
        {
            UInt32 pieceNumber = 0;

            pieceNumber = unpackUInt32(messageBody, 1);

            if (!remotePeer.FileDownloader.ReceivedMap[pieceNumber]) {
                remotePeer.FileDownloader.RemotePeerMap[pieceNumber] = true;
                remotePeer.FileDownloader.RemotePeerMapEnries++;
                PWP.interested(remotePeer);
            }

            Console.WriteLine($"Have piece= {pieceNumber}");
        }
       
        private static void handleBITMAP(Peer remotePeer, byte[] messageBody)
        {
            byte[] usageMap = new byte [messageBody.Length-1];

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

        }

        private static void handleREQUEST(Peer remotePeer, byte[] messageBody)
        {

        }

        private static void handlePIECE(Peer remotePeer, byte[] messageBody)
        {
            UInt32 pieceNumber = unpackUInt32(messageBody, 1);
            UInt32 blockOffset = unpackUInt32(messageBody, 5);

            Console.WriteLine($"Piece {pieceNumber} Block Offset {blockOffset} Data Size {messageBody.Length-9}\n");
            
        }

        private static void handleCANCEL(Peer remotePeer, byte[] messageBody)
        {

        }

        public static void readRemotePeerMessages(Peer remotePeer, NetworkStream peerStream)
        {
            byte[] messageLength = new byte[4];
            UInt32 convertedLength = 0;

            peerStream.Read(messageLength, 0, messageLength.Length);

            convertedLength = unpackUInt32(messageLength, 0);

            if (convertedLength > 0)
            {

                byte[] messageBody = new byte[convertedLength];

                peerStream.Read(messageBody, 0, (int)convertedLength);

                switch (messageBody[0])
                {
                    case 0x0:
                        Console.WriteLine("CHOKE");
                        handleCHOKE(remotePeer, messageBody);
                        break;
                    case 0x1:
                        Console.WriteLine("UNCHOKE");
                        handleUNCHOKE(remotePeer, messageBody);
                        break;
                    case 0x2:
                        Console.WriteLine("INTERESTED");
                        handleINTERESTED(remotePeer, messageBody);
                        break;
                    case 0x3:
                        Console.WriteLine("UNINTERESTED");
                        handleUNINTERESTED(remotePeer, messageBody);
                        break;
                    case 0x4:
                        Console.WriteLine("HAVE");
                        handleHAVE(remotePeer, messageBody);
                        break;
                    case 0x5:
                        Console.WriteLine("BITFIELD");
                        handleBITMAP(remotePeer, messageBody);
                        break;
                    case 0x6:
                        Console.WriteLine("REQUEST");
                        handleREQUEST(remotePeer, messageBody);
                        break;
                    case 0x7:
                        Console.WriteLine("PIECE");
                        handlePIECE(remotePeer, messageBody);
                        break;
                    case 0x8:
                        Console.WriteLine("CANCEL");
                        handleCANCEL(remotePeer, messageBody);
                        break;
                    default:
                        Console.WriteLine($"UNKOWN {messageBody[0]}");
                        break;
                }
            }
            Thread.Sleep(1000);
        }

        public static void choke(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(0);

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

        public static void unchoke(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(1);

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

        public static void interested(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(2);

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

        public static void uninterested(Peer remotePeer)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(1));
            requestPacket.Add(3);

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

        public static void have(Peer remotePeer, int pieceNumber)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(5));
            requestPacket.Add(4);
            requestPacket.AddRange(packUInt32(pieceNumber));

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

        public static void have(Peer remotePeer, byte[] bitField)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(bitField.Length+1));
            requestPacket.Add(5);

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

        public static void request(Peer remotePeer, int pieceNumber, int blockOffset, int blockSize)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(13));
            requestPacket.Add(6);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(packUInt32(blockSize));

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

         public static void piece(Peer remotePeer, int pieceNumber, int blockOffset, byte[] blockData)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(9+blockData.Length));
            requestPacket.Add(7);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(blockData);

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }

        public static void cancel(Peer remotePeer, int pieceNumber, int blockOffset, byte[] blockData)
        {
            List<byte> requestPacket = new List<byte>();

            requestPacket.AddRange(packUInt32(9 + blockData.Length));
            requestPacket.Add(8);
            requestPacket.AddRange(packUInt32(pieceNumber));
            requestPacket.AddRange(packUInt32(blockOffset));
            requestPacket.AddRange(blockData);

            remotePeer.PeerStream.Write(requestPacket.ToArray(), 0, requestPacket.Count);

        }
    }
}
