//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide all the necessary functionality for communication 
// with remote trackers using HTTP/UDP.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;

namespace BitTorrentLibrary
{
    /// <summary>
    /// Tracker class.
    /// </summary>
    public class TrackerUDP :Tracker
    {
        private readonly Random _transIDGenerator = new Random();
        private bool _connected = false;
        private UInt64 _connectionID;
        private readonly UdpClient udpConnection;
        private IPEndPoint _connectionEndPoint;

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
        private void PackUInt32(List<byte> buffer, UInt32 value)
        {
            buffer.Add((byte)(value >> 24));
            buffer.Add((byte)(value >> 16));
            buffer.Add((byte)(value >> 8));
            buffer.Add((byte)(value));
        }
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
        private UInt32 UnPackUInt32(byte[] buffer, UInt32 offset)
        {
            UInt32 value = ((UInt32)buffer[offset]) << 24;
            value |= ((UInt32)buffer[offset + 1]) << 16;
            value |= ((UInt32)buffer[offset + 2]) << 8;
            value |= ((UInt32)buffer[offset + 3]);
            return value;
        }

        private void Connect()
        {
            try
            {
                UInt32 transactionID = (UInt32)_transIDGenerator.Next();
                List<byte> connectPacket = new List<byte>();

                PackUInt64(connectPacket, 0x41727101980);
                PackUInt32(connectPacket, 0);
                PackUInt32(connectPacket, transactionID);

                udpConnection.Connect(_connectionEndPoint);
                udpConnection.Send(connectPacket.ToArray(), connectPacket.Count);
                byte[] connectReply = udpConnection.Receive(ref _connectionEndPoint);
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
                if (!_connected) {
                    throw new Error("BitTorrent (TrackerUDP) Error : Could not connect to UDP tracker server.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
                throw new Error("BitTorrent (TrackerUDP) Error : " + ex.Message);
            }
        }
        public AnnounceResponse Announce()
        {
            Log.Logger.Info($"Announce: info_hash={Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(_infoHash, 0, _infoHash.Length))} " +
                  $"peer_id={_peerID} port={_port} compact={_compact} no_peer_id={_noPeerID} uploaded={Uploaded}" +
                  $"downloaded={Downloaded} left={Left} event={EventString[(int)Event]} ip={_ip} key={_key} trackerid={_trackerID} numwanted={_numWanted}");

            AnnounceResponse response = new AnnounceResponse
            {
                peers = new List<PeerDetails>()
            };

            try
            {
                if (!_connected) {
                    Connect();
                }

                List<byte> announcePacket = new List<byte>();
                UInt32 transactionID = (UInt32)_transIDGenerator.Next();

                PackUInt64(announcePacket, _connectionID);
                PackUInt32(announcePacket, 1);
                PackUInt32(announcePacket, transactionID);
                announcePacket.AddRange(_infoHash);
                announcePacket.AddRange(Encoding.ASCII.GetBytes(_peerID));
                PackUInt64(announcePacket, Downloaded);
                PackUInt64(announcePacket, Left);
                PackUInt64(announcePacket, Uploaded);
                PackUInt32(announcePacket, (UInt32)Event); // event
                PackUInt32(announcePacket, 0);             // ip
                PackUInt32(announcePacket, 0);             // key
                PackUInt32(announcePacket, _numWanted);
                PackUInt32(announcePacket, _port);
                PackUInt32(announcePacket, 0);             // Extensions.

                udpConnection.Send(announcePacket.ToArray(), announcePacket.Count);
                byte[] announceReply = udpConnection.Receive(ref _connectionEndPoint);

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
                        if (peer.ip != _ip) // Ignore self in peers list
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
                        byte [] errorMessage = new byte [announceReply.Length-4];
                        announceReply.CopyTo(errorMessage, 4);
                        response.failure=true;
                        response.statusMessage =errorMessage.ToString();
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
        /// <summary>
        /// Restart announce on interval changing and save minimum interval and trackr ID.
        /// </summary>
        /// <param name="response"></param>
        private void UpdateRunningStatusFromAnnounce(AnnounceResponse response)
        {
            try
            {
                _trackerID = response.trackerID;
                _minInterval = response.minInterval;

                if (response.interval > _minInterval)
                {
                    UInt32 oldInterval = _interval;
                    _interval = response.interval;
                    if (oldInterval != _interval)
                    {
                        StopAnnouncing();
                        StartAnnouncing();
                    }
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Tracker): " + ex.Message);
            }
        }
        /// <summary>
        /// On  announce event send announce request to tracker and get response.
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="e">E.</param>
        /// <param name="tracker">Tracker.</param>
        private static void OnAnnounceEvent(TrackerUDP tracker)
        {
            AnnounceResponse response = tracker.Announce();
            tracker._updatePeerSwarm?.Invoke(response.peers);
            tracker.UpdateRunningStatusFromAnnounce(response);
        }
        // /// <summary>
        // /// Is a specified tracker supported.
        // /// </summary>
        // /// <param name="trackerURL"></param>
        // /// <returns>==true tracker supported</returns>
        // public bool IsSupported(string trackerURL)
        // {
        //     return trackerURL.StartsWith("udp://");
        // }
        /// <summary>
        /// Initialise BitTorrent Tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        /// <param name="infoHash"></param>
        /// <param name="updatePeerSwarm"></param>
        public TrackerUDP(string trackerURL, byte[] infoHash, UpdatePeers updatePeerSwarm = null)  : base(trackerURL, infoHash, updatePeerSwarm)
        {
            udpConnection = new UdpClient();
            udpConnection.Client.ReceiveTimeout = 15000;
            Uri trackerURI = new Uri(_trackerURL);
            IPAddress[] trackerAddress = Dns.GetHostAddresses(trackerURI.Host);
            _connectionEndPoint = new IPEndPoint(trackerAddress[0], (int)trackerURI.Port);
        }
        /// <summary>
        /// Change tracker status.
        /// </summary>
        public void ChangeStatus(TrackerEvent status)
        {
            _announceTimer?.Stop();
            Event = status;
            OnAnnounceEvent(this);
            Event = TrackerEvent.None;  // Reset it back to default on next tick
            _announceTimer?.Start();
        }
        /// <summary>
        /// Starts the announce requests to tracker.
        /// </summary>
        public void StartAnnouncing()
        {
            try
            {
                if (_announceTimer != null)
                {
                    StopAnnouncing();
                }
                _announceTimer = new System.Timers.Timer(_interval);
                _announceTimer.Elapsed += (sender, e) => OnAnnounceEvent(this);
                _announceTimer.AutoReset = true;
                _announceTimer.Enabled = true;
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Tracker): " + ex.Message);
            }
        }

        /// <summary>
        /// Stop announcing to tracker..
        /// </summary>
        public void StopAnnouncing()
        {
            try
            {
                if (_announceTimer != null)
                {
                    _announceTimer.Stop();
                    _announceTimer.Dispose();
                    _announceTimer = null;
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent Error (Tracker): " + ex.Message);
            }
        }
    }
}