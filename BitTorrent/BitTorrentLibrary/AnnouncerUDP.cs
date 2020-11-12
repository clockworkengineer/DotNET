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

    /// <summary>
    /// UDP Announcer
    /// </summary>
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
        private readonly UdpClient _trackerConnection;              // Tracker UDP connection
        private IPEndPoint _trackerEndPoint;                       // Tracker enpoint

        /// <summary>
        /// Send and recieve command to UDP tracker. If we get a timeout then the
        /// standard says keep retrying for 60 seconds.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private byte[] SendCommand(byte[] command)
        {

            UInt32 retries = 0;
            while (true)
            {
                try
                {
                    _trackerConnection.Send(command, command.Length);
                    return _trackerConnection.Receive(ref _trackerEndPoint);
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
        /// Connect to UDP tracker server.
        /// </summary>
        private void Connect()
        {
            try
            {
                UInt32 transactionID = (UInt32)_transIDGenerator.Next();
                List<byte> connectPacket = new List<byte>();

                connectPacket.AddRange(Util.PackUInt64(0x41727101980));
                connectPacket.AddRange(Util.PackUInt32((UInt32)UDPAction.Connect));
                connectPacket.AddRange(Util.PackUInt32(transactionID));

                var connectReply = SendCommand(connectPacket.ToArray());
                if (connectReply.Length == 16)
                {
                    if (Util.UnPackUInt32(connectReply, 0) == (UInt32)UDPAction.Connect)
                    {
                        if (transactionID == Util.UnPackUInt32(connectReply, 4))
                        {
                            _connectionID = Util.UnPackUInt64(connectReply, 8);
                            _connected = true;
                            Log.Logger.Info("Connected to UDP Tracker.");
                        }
                    }
                    else if (Util.UnPackUInt32(connectReply, 0) == (UInt32)UDPAction.Error)
                    {
                        if (transactionID == Util.UnPackUInt32(connectReply, 4))
                        {
                            byte[] errorMessage = new byte[connectReply.Length - 4];
                            connectReply.CopyTo(errorMessage, 4);
                            throw new Exception("UDP connect returned error : " + errorMessage.ToString());
                        }
                    }
                }

                if (!_connected)
                {
                    throw new Exception("Could not connect to UDP tracker server.");
                }
            }
            catch (Exception)
            {
                throw;
            }

        }
        /// <summary>
        /// Setup data and resources needed by UDP tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        public AnnouncerUDP(string trackerURL)
        {
            _trackerConnection = new UdpClient();
            _trackerConnection.Client.ReceiveTimeout = 15000;   // 15 seconds
            Uri trackerURI = new Uri(trackerURL);
            IPAddress[] trackerAddress = Dns.GetHostAddresses(trackerURI.Host);
            _trackerEndPoint = new IPEndPoint(trackerAddress[0], (int)trackerURI.Port);
            _trackerConnection.Connect(_trackerEndPoint);
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
                peers = new List<PeerDetails>()
            };

            try
            {
                if (!_connected)
                {
                    Connect();
                }

                List<byte> announcePacket = new List<byte>();
                UInt32 transactionID = (UInt32)_transIDGenerator.Next();

                announcePacket.AddRange(Util.PackUInt64(_connectionID));
                announcePacket.AddRange(Util.PackUInt32((UInt32)UDPAction.Announce));
                announcePacket.AddRange(Util.PackUInt32(transactionID));
                announcePacket.AddRange(tracker.InfoHash);
                announcePacket.AddRange(Encoding.ASCII.GetBytes(tracker.PeerID));
                announcePacket.AddRange(Util.PackUInt64(tracker.Downloaded));
                announcePacket.AddRange(Util.PackUInt64(tracker.Left));
                announcePacket.AddRange(Util.PackUInt64(tracker.Uploaded));
                announcePacket.AddRange(Util.PackUInt32((UInt32)tracker.Event));
                announcePacket.AddRange(Util.PackUInt32(0));                          // ip
                announcePacket.AddRange(Util.PackUInt32(0));                          // key
                announcePacket.AddRange(Util.PackUInt32((UInt32)tracker.NumWanted));
                announcePacket.AddRange(Util.PackUInt32(tracker.Port));
                announcePacket.AddRange(Util.PackUInt32(0));                          // Extensions.

                var announceReply = SendCommand(announcePacket.ToArray());

                if (Util.UnPackUInt32(announceReply, 0) == (UInt32)UDPAction.Announce)
                {
                    if (transactionID == Util.UnPackUInt32(announceReply, 4))
                    {
                        response.interval = Util.UnPackUInt32(announceReply, 8);
                        response.incomplete = Util.UnPackUInt32(announceReply, 12);
                        response.complete = Util.UnPackUInt32(announceReply, 16);
                    }
                    // Only send more peers once the last have been processed
                    if (tracker.peerSwarmQueue.Count == 0)
                    {
                        for (var num = 20; num < announceReply.Length; num += 6)
                        {
                            PeerDetails peer = new PeerDetails
                            {
                                infoHash = tracker.InfoHash,
                                peerID = String.Empty,
                                ip = $"{announceReply[num]}.{announceReply[num + 1]}.{announceReply[num + 2]}.{announceReply[num + 3]}"
                            };
                            peer.port = ((UInt32)announceReply[num + 4] * 256) + announceReply[num + 5];
                            if (peer.ip != tracker.Ip) // Ignore self in peers list
                            {
                                Log.Logger.Info($"Peer {peer.ip} Port {peer.port} found.");
                                response.peers.Add(peer);
                            }
                        }
                    }

                }
                else if (Util.UnPackUInt32(announceReply, 0) == (UInt32)UDPAction.Error)
                {
                    if (transactionID == Util.UnPackUInt32(announceReply, 4))
                    {
                        byte[] errorMessage = new byte[announceReply.Length - 4];
                        announceReply.CopyTo(errorMessage, 4);
                        response.failure = true;
                        response.statusMessage = errorMessage.ToString();
                    }
                }
                else
                {
                    throw new Exception($"Invalid announce response {Util.UnPackUInt32(announceReply, 0)}.");
                }
            }

            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
                throw;
            }

            return response;
        }
    }
}
