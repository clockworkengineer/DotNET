//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Perform UDP annouce requests in remote tracker.
//
// Copyright 2019.
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
    public class AnnouncerUDP : IAnnouncer
    {
        private readonly Random _transIDGenerator = new Random();   // Transaction ID generator
        private bool _connected = false;                            // == true connected
        private UInt64 _connectionID;                               // Returned connection ID
        private readonly UdpClient _trackerConnection;              // Tracker UDP connection
        private IPEndPoint _trackerEndPoint;                        // Tracker enpoint

        /// <summary>
        /// Pack UInt64 MSB first into 8 bytes buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value"></param>
        private void PackUInt64(List<byte> buffer, UInt64 value)
        {
            buffer.Add((byte)(value >> 56));
            buffer.Add((byte)(value >> 48));
            buffer.Add((byte)(value >> 40));
            buffer.Add((byte)(value >> 32));
            buffer.Add((byte)(value >> 24));
            buffer.Add((byte)(value >> 16));
            buffer.Add((byte)(value >> 8));
            buffer.Add((byte)(value));
        }
        /// <summary>
        /// Pack UInt32 MSB first into 8 bytes buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value"></param>
        private void PackUInt32(List<byte> buffer, UInt32 value)
        {
            buffer.Add((byte)(value >> 24));
            buffer.Add((byte)(value >> 16));
            buffer.Add((byte)(value >> 8));
            buffer.Add((byte)(value));
        }
        /// <summary>
        /// Unpack into a UInt64 MSB from an byte buffer at a given offset.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private UInt64 UnPackUInt64(byte[] buffer, UInt32 offset)
        {
            UInt64 value = ((UInt64)buffer[offset]) << 56;
            value |= ((UInt64)buffer[offset + 1]) << 48;
            value |= ((UInt64)buffer[offset + 2]) << 40;
            value |= ((UInt64)buffer[offset + 3]) << 32;
            value |= ((UInt64)buffer[offset + 4]) << 24;
            value |= ((UInt64)buffer[offset + 5]) << 16;
            value |= ((UInt64)buffer[offset + 6]) << 8;
            value |= ((UInt64)buffer[offset + 7]);
            return value;
        }
        /// <summary>
        /// Unpack into a UInt32 MSB from an byte buffer at a given offset.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private UInt32 UnPackUInt32(byte[] buffer, UInt32 offset)
        {
            UInt32 value = ((UInt32)buffer[offset]) << 24;
            value |= ((UInt32)buffer[offset + 1]) << 16;
            value |= ((UInt32)buffer[offset + 2]) << 8;
            value |= ((UInt32)buffer[offset + 3]);
            return value;
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

                PackUInt64(connectPacket, 0x41727101980);
                PackUInt32(connectPacket, 0);
                PackUInt32(connectPacket, transactionID);

                _trackerConnection.Connect(_trackerEndPoint);
                _trackerConnection.Send(connectPacket.ToArray(), connectPacket.Count);
                byte[] connectReply = _trackerConnection.Receive(ref _trackerEndPoint);
                if (connectReply.Length == 16)
                {
                    if (UnPackUInt32(connectReply, 0) == 0)
                    {
                        if (transactionID == UnPackUInt32(connectReply, 4))
                        {
                            _connectionID = UnPackUInt64(connectReply, 8);
                            _connected = true;
                            Log.Logger.Info("Connected to UDP Tracker.");
                        }
                    }
                }
                if (!_connected)
                {
                    throw new Error("BitTorrent (TrackerUDP) Error : Could not connect to UDP tracker server.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
                throw new Error("BitTorrent (TrackerUDP) Error : " + ex.Message);
            }
        }
        /// <summary>
        /// Intialise instance of UDP tracker
        /// </summary>
        /// <param name="trackerURL"></param>
        public AnnouncerUDP(string trackerURL)
        {
            _trackerConnection = new UdpClient();
            _trackerConnection.Client.ReceiveTimeout = 15000;
            Uri trackerURI = new Uri(trackerURL);
            IPAddress[] trackerAddress = Dns.GetHostAddresses(trackerURI.Host);
            _trackerEndPoint = new IPEndPoint(trackerAddress[0], (int)trackerURI.Port);
        }
        /// <summary>
        /// Perform an announce request to tracker and return any response.
        /// </summary>
        /// <param name="tracker"></param>
        /// <returns>Announce response</returns>
        public AnnounceResponse Announce(Tracker tracker)
        {
            Log.Logger.Info($"Announce: info_hash={Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(tracker.InfoHash, 0, tracker.InfoHash.Length))} " +
                  $"peer_id={tracker.PeerID} port={tracker.Port} compact={tracker.Compact} no_peer_id={tracker.NoPeerID} uploaded={tracker.Uploaded}" +
                  $"downloaded={tracker.Downloaded} left={tracker.Left} event={Tracker.EventString[(int)tracker.Event]} ip={tracker.Ip} key={tracker.Key}" +
                  $"trackerid={tracker.TrackerID} numwanted={tracker.NumWanted}");

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

                PackUInt64(announcePacket, _connectionID);
                PackUInt32(announcePacket, 1);
                PackUInt32(announcePacket, transactionID);
                announcePacket.AddRange(tracker.InfoHash);
                announcePacket.AddRange(Encoding.ASCII.GetBytes(tracker.PeerID));
                PackUInt64(announcePacket, tracker.Downloaded);
                PackUInt64(announcePacket, tracker.Left);
                PackUInt64(announcePacket, tracker.Uploaded);
                PackUInt32(announcePacket, (UInt32)tracker.Event);      // event
                PackUInt32(announcePacket, 0);                          // ip
                PackUInt32(announcePacket, 0);                          // key
                PackUInt32(announcePacket, (UInt32)tracker.NumWanted);
                PackUInt32(announcePacket, tracker.Port);
                PackUInt32(announcePacket, 0);                          // Extensions.

                _trackerConnection.Send(announcePacket.ToArray(), announcePacket.Count);
                byte[] announceReply = _trackerConnection.Receive(ref _trackerEndPoint);

                if (UnPackUInt32(announceReply, 0) == 1)
                {
                    if (transactionID == UnPackUInt32(announceReply, 4))
                    {
                        response.interval = UnPackUInt32(announceReply, 8);
                        response.incomplete = UnPackUInt32(announceReply, 12);
                        response.complete = UnPackUInt32(announceReply, 16);
                    }
                    for (var num = 20; num < announceReply.Length; num += 6)
                    {
                        PeerDetails peer = new PeerDetails
                        {
                            _peerID = String.Empty,
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
                else if (UnPackUInt32(announceReply, 0) == 2)
                {
                    if (transactionID == UnPackUInt32(announceReply, 4))
                    {
                        byte[] errorMessage = new byte[announceReply.Length - 4];
                        announceReply.CopyTo(errorMessage, 4);
                        response.failure = true;
                        response.statusMessage = errorMessage.ToString();
                    }
                }
                else
                {
                    throw new Error("(BitTorrent (TrackerUDP) Error : Invalid announce response.");
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
                throw new Error("BitTorrent (TrackerUDP) Error : " + ex.Message);
            }

            return response;
        }
    }
}
