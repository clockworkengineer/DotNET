//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Perform UDP announce requests to remote tracker. Details on the protocol
// can be found at https://libtorrent.org/udp_tracker_protocol.html or by seaching 
// the net for "udp tracker protocol specification". Note: At present scraping and the 
// security packet trailer are not supported.
//
// Copyright 2020.
//
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace BitTorrentLibrary
{

    internal class AnnouncerUDP : IAnnouncer
    {
        private enum UDPAction
        {
            Connect = 0,
            Announce,
            Scrape,
            Error
        }
        private readonly Random _transIDGenerator = new Random();   // Transaction ID generator
        private bool _connected = false;                            // == true connected
        private UInt64 _connectionID;                               // Returned connection ID
        private readonly IUDP _udp;                                 // UDP connection
        /// <summary>
        /// Send and recieve command to UDP tracker. If we get a timeout then the
        /// standard says keep retrying for 60 seconds; with the timeout being 15 seconds.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private byte[] SendCommand(byte[] command)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    _udp.Send(command);
                    return _udp.Receive();
                }
                catch (SocketException ex)
                {
                    if ((ex.ErrorCode == 110) && (retries < 3))
                    {
                        retries++;
                        continue;
                    }
                    throw;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
        /// <summary>
        /// Build connect command packet.
        /// </summary>
        /// <param name="transactionID"></param>
        /// <returns></returns>
        private byte[] BuildConnectPacket(UInt32 transactionID)
        {
            List<byte> commandPacket = new List<byte>();
            commandPacket.AddRange(Util.PackUInt64(0x41727101980));
            commandPacket.AddRange(Util.PackUInt32((UInt32)UDPAction.Connect));
            commandPacket.AddRange(Util.PackUInt32((UInt32)transactionID));
            return commandPacket.ToArray();
        }
        /// <summary>
        /// Build announce command packet
        /// </summary>
        /// <param name="tracker"></param>
        /// <param name="transactionID"></param>
        /// <returns></returns>
        private byte[] BuildAnnouncePacket(Tracker tracker, UInt32 transactionID)
        {
            List<byte> commandPacket = new List<byte>();
            commandPacket.AddRange(Util.PackUInt64(_connectionID));
            commandPacket.AddRange(Util.PackUInt32((UInt32)UDPAction.Announce));
            commandPacket.AddRange(Util.PackUInt32(transactionID));
            commandPacket.AddRange(tracker.InfoHash);
            commandPacket.AddRange(Encoding.ASCII.GetBytes(tracker.PeerID));
            commandPacket.AddRange(Util.PackUInt64(tracker.Downloaded));
            commandPacket.AddRange(Util.PackUInt64(tracker.Left));
            commandPacket.AddRange(Util.PackUInt64(tracker.Uploaded));
            commandPacket.AddRange(Util.PackUInt32((UInt32)tracker.Event));
            commandPacket.AddRange(Util.PackUInt32(0));                          // ip
            commandPacket.AddRange(Util.PackUInt32(0));                          // key
            commandPacket.AddRange(Util.PackUInt32((UInt32)tracker.NumWanted));
            commandPacket.AddRange(Util.PackUInt32((UInt32)tracker.Port));
            commandPacket.AddRange(Util.PackUInt32(0));                          // Extensions.
            return commandPacket.ToArray();
        }
        /// <summary>
        /// Connect to UDP tracker server.
        /// </summary>
        private void Connect()
        {
            int transactionID = (int)_transIDGenerator.Next();
            var commandReply = SendCommand(BuildConnectPacket((UInt32)transactionID));
            if (transactionID == Util.UnPackUInt32(commandReply, 4))
            {
                if (Util.UnPackUInt32(commandReply, 0) == (UInt32)UDPAction.Connect)
                {
                    _connectionID = Util.UnPackUInt64(commandReply, 8);
                    _connected = true;
                    Log.Logger.Info("Connected to UDP Tracker.");
                }
                else if (Util.UnPackUInt32(commandReply, 0) == (UInt32)UDPAction.Error)
                {
                    byte[] errorMessage = new byte[commandReply.Length - 4];
                    commandReply.CopyTo(errorMessage, 4);
                    throw new Exception("UDP connect returned error : " + errorMessage.ToString());
                } else {
                    throw new Exception($"Invalid UDP connect response {Util.UnPackUInt32(commandReply, 0)}.");
                }
            }
            else
            {
                throw new Exception("Transaction ID for reply does not agree with that sent with connect.");
            }

        }
        /// <summary>
        /// Setup data and resources needed by UDP tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        /// <param name="udp"></param>
        public AnnouncerUDP(string trackerURL, IUDP udp)
        {
            _udp = udp;
            _udp.Connect(trackerURL);
        }
        /// <summary>
        /// Perform an announce request to tracker and return any response.
        /// </summary>
        /// <param name="tracker"></param>
        /// <returns>Announce response</returns>
        public AnnounceResponse Announce(Tracker tracker)
        {
            Tracker.LogAnnouce(tracker);
            AnnounceResponse response = new AnnounceResponse
            {
                peerList = new List<PeerDetails>()
            };
            try
            {
                if (!_connected)
                {
                    Connect();
                }
                UInt32 transactionID = (UInt32)_transIDGenerator.Next();
                var commandReply = SendCommand(BuildAnnouncePacket(tracker, (UInt32)transactionID));
                if (transactionID == Util.UnPackUInt32(commandReply, 4))
                {
                    if (Util.UnPackUInt32(commandReply, 0) == (UInt32)UDPAction.Announce)
                    {
                        response.interval = (int)Util.UnPackUInt32(commandReply, 8);
                        response.incomplete = (int)Util.UnPackUInt32(commandReply, 12);
                        response.complete = (int)Util.UnPackUInt32(commandReply, 16);
                        response.peerList = tracker.GetCompactPeerList(commandReply, 20);
                    }
                    else if (Util.UnPackUInt32(commandReply, 0) == (UInt32)UDPAction.Error)
                    {
                        byte[] errorMessage = new byte[commandReply.Length - 4];
                        commandReply.CopyTo(errorMessage, 4);
                        response.failure = true;
                        response.statusMessage = errorMessage.ToString();
                    }
                    else
                    {
                        throw new Exception($"Invalid UDP announce response {Util.UnPackUInt32(commandReply, 0)}.");
                    }
                }
                else
                {
                    throw new Exception("Transaction ID for reply does not agree with that sent with announce.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                throw;
            }
            return response;
        }
    }
}
