//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Perform HTTP announce requests to remote tracker.
//
// Copyright 2020.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
namespace BitTorrentLibrary
{
    /// <summary>
    /// HTTP Announcer
    /// </summary>
    internal class AnnouncerHTTP : IAnnouncer
    {
        /// <summary>
        /// Decodes the announce request BEncoded response recieved from a tracker.
        /// </summary>
        /// <param name="announceResponse">Announce response.</param>
        /// <param name="decodedResponse">Response.</param>
        private void DecodeAnnounceResponse(Tracker tracker, byte[] announceResponse, ref AnnounceResponse decodedResponse)
        {
            if (announceResponse.Length != 0)
            {
                BNodeBase decodedAnnounce = Bencoding.Decode(announceResponse);
                decodedResponse.statusMessage = Bencoding.GetDictionaryEntryString(decodedAnnounce, "failure reason");
                if (decodedResponse.statusMessage != "")
                {
                    decodedResponse.failure = true;
                    return; // If failure present then ignore rest of reply.
                }
                int.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "complete"), out decodedResponse.complete);
                int.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "incomplete"), out decodedResponse.incomplete);
                BNodeBase field = Bencoding.GetDictionaryEntry(decodedAnnounce, "peers");
                if (field != null)
                {
                    decodedResponse.peers = new List<PeerDetails>();
                    if (field is BNodeString bNodeString) // Compact peer list reply
                    {
                        byte[] peers = (bNodeString).str;
                        for (var num = 0; num < peers.Length; num += 6)
                        {
                            PeerDetails peer = new PeerDetails
                            {
                                infoHash = tracker.InfoHash,
                                peerID = String.Empty,
                                ip = $"{peers[num]}.{peers[num + 1]}.{peers[num + 2]}.{peers[num + 3]}"
                            };
                            peer.port = ((int)peers[num + 4] * 256) + peers[num + 5];
                            if (peer.ip != tracker.Ip) // Ignore self in peers list
                            {
                                Log.Logger.Trace($"(Tracker) Peer {peer.ip} Port {peer.port} found.");
                                decodedResponse.peers.Add(peer);
                            }
                        }
                    }
                    else if (field is BNodeList bNodeList)  // Non-compact peer list reply
                    {
                        foreach (var listItem in (bNodeList).list)
                        {
                            if (listItem is BNodeDictionary bNodeDictionary)
                            {
                                PeerDetails peer = new PeerDetails
                                {
                                    infoHash = tracker.InfoHash
                                };
                                BNodeBase peerDictionaryItem = (bNodeDictionary);
                                BNodeBase peerField = Bencoding.GetDictionaryEntry(peerDictionaryItem, "ip");
                                if (peerField != null)
                                {
                                    peer.ip = Encoding.ASCII.GetString(((BitTorrentLibrary.BNodeString)peerField).str);
                                }
                                if (peer.ip.Contains(":"))
                                {
                                    peer.ip = peer.ip.Substring(peer.ip.LastIndexOf(":", StringComparison.Ordinal) + 1);
                                }
                                peerField = Bencoding.GetDictionaryEntry(peerDictionaryItem, "port");
                                if (peerField != null)
                                {
                                    peer.port = int.Parse(Encoding.ASCII.GetString(((BitTorrentLibrary.BNodeNumber)peerField).number));
                                }
                                if (peer.ip != tracker.Ip) // Ignore self in peers list
                                {
                                    Log.Logger.Trace($"(Tracker) Peer {peer.ip} Port {peer.port} found.");
                                    decodedResponse.peers.Add(peer);
                                }
                            }
                        }
                    }
                }
                int.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "interval"), out decodedResponse.interval);
                int.TryParse(Bencoding.GetDictionaryEntryString(decodedAnnounce, "min interval"), out decodedResponse.minInterval);
                decodedResponse.trackerID = Bencoding.GetDictionaryEntryString(decodedAnnounce, "tracker id");
                decodedResponse.statusMessage = Bencoding.GetDictionaryEntryString(decodedAnnounce, "warning message");
                decodedResponse.announceCount++;
            }
        }
        /// <summary>
        /// Build url string used for announce.
        /// </summary>
        /// <param name="tracker"></param>
        /// <returns></returns>
        private string BuildAnnouceURL(Tracker tracker)
        {
            string announceURL = $"{tracker.TrackerURL}?info_hash=" +
                  $"{Encoding.ASCII.GetString(WebUtility.UrlEncodeToBytes(tracker.InfoHash, 0, tracker.InfoHash.Length))}" +
                  $"&peer_id={tracker.PeerID}&port={tracker.Port}&compact={tracker.Compact}" +
                  $"&no_peer_id={tracker.NoPeerID}&uploaded={tracker.Uploaded}&downloaded={tracker.Downloaded}" +
                  $"&left={tracker.Left}&ip={tracker.Ip}&key={tracker.Key}&trackerid={tracker.TrackerID}&numwanted={tracker.NumWanted}";
            // Some trackers require no event present if its value is none
            if (tracker.Event != TrackerEvent.None)
            {
                announceURL += $"&event={Tracker.EventString[(int)tracker.Event]}";
            }
            return announceURL;
        }
        /// <summary>
        /// Setup data and resources needed by HTTP tracker.
        /// </summary>
        /// <param name="trackerURL"></param>
        public AnnouncerHTTP(string _)
        {
        }
        /// <summary>
        /// Perform an HTTP announce request to tracker and return any response.
        /// </summary>
        /// <param name="tracker"></param>
        /// <returns>Announce response.</returns>
        public AnnounceResponse Announce(Tracker tracker)
        {
            Tracker.LogAnnouce(tracker);
            AnnounceResponse response = new AnnounceResponse();
            try
            {
                string announceURL = BuildAnnouceURL(tracker);
                HttpWebRequest httpGetRequest = WebRequest.Create(announceURL) as HttpWebRequest;
                httpGetRequest.Method = "GET";
                httpGetRequest.ContentType = "text/xml";
                using (HttpWebResponse httpGetResponse = httpGetRequest.GetResponse() as HttpWebResponse)
                {
                    StreamReader reader = new StreamReader(httpGetResponse.GetResponseStream());
                    byte[] announceResponse;
                    using (var memstream = new MemoryStream())
                    {
                        reader.BaseStream.CopyTo(memstream);
                        announceResponse = memstream.ToArray();
                    }
                    if (httpGetResponse.StatusCode == HttpStatusCode.OK)
                    {
                        DecodeAnnounceResponse(tracker, announceResponse, ref response);
                    }
                    else
                    {
                        throw new BitTorrentError(httpGetResponse.StatusDescription);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw;
            }
            return response;
        }
    }
}
