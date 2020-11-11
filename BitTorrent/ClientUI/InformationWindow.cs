//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Information download window. This is includes  torrent information 
// like a list of peers in its swarm, bytes uploaded, bytes downloaded and infohash.
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BitTorrentLibrary;
using Terminal.Gui;

namespace ClientUI
{
    /// <summary>
    /// Torrent information window.
    /// </summary>
    public class InformationWindow : Window
    {
        private readonly Label _trackerLabel;         // Label for tracker text field
        private readonly ListView _peersListView;     // List view holding downloaded peer swarm
        private readonly Window _peersWindow;         // Peer swarm list sub-window
        private readonly TextField[] _infoTextFields; // Torrent information window text fields
        public TextField TrackerText { get; }         // Torrent tracker text field


        /// <summary>
        /// Convert torrent infohash to string.
        /// </summary>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        private static string InfoHashToString(byte[] infoHash)
        {
            StringBuilder hex = new StringBuilder(infoHash.Length * 2);
            foreach (byte b in infoHash)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString().ToLower();
        }

        /// <summary>
        /// Build information window.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public InformationWindow(string name) : base(name)
        {
            List<View> viewables = new List<View>();

            _trackerLabel = new Label("Tracker : ")
            {
                X = 1,
                Y = 1
            };
            viewables.Add(_trackerLabel);
            TrackerText = new TextField()
            {
                X = Pos.Right(_trackerLabel),
                Y = Pos.Top(_trackerLabel),
                Width = 70,
                CanFocus = false
            };
            viewables.Add(TrackerText);
            _peersWindow = new Window("Peers")
            {
                X = Pos.Left(_trackerLabel),
                Y = Pos.Bottom(_trackerLabel) + 1,
                Width = 23,
                Height = Dim.Fill(),
            };
            viewables.Add(_peersWindow);

            _peersListView = new ListView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = false
            };
            _peersWindow.Add(_peersListView);

            string[] labels = { "InfoHash:", "Downloaded:", "Uploaded:", "Missing:", "Status:", "Swarm:", "Dead:", "Tracker:" };

            _infoTextFields = new TextField[labels.Length];

            int pos = 0;
            foreach (var label in labels)
            {
                Label text = new Label(label)
                {
                    X = Pos.Right(_peersWindow) + 1,
                    Y = Pos.Top(_peersWindow) + pos
                };
                viewables.Add(text);
                _infoTextFields[pos] = new TextField()
                {
                    X = Pos.Right(_peersWindow) + 12,
                    Y = Pos.Bottom(_trackerLabel) + pos + 1,
                    Width = 40,
                    CanFocus = false
                };
                viewables.Add(_infoTextFields[pos]);
                pos++;
            }

            foreach (var viewable in viewables)
            {
                Add(viewable);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="torrentDetails"></param>
        public void UpdateInformation(TorrentDetails torrentDetails)
        {
            _infoTextFields[0].Text = InfoHashToString(torrentDetails.infoHash);
            _infoTextFields[1].Text = torrentDetails.downloadedBytes.ToString();
            _infoTextFields[2].Text = torrentDetails.uploadedBytes.ToString();
            _infoTextFields[3].Text = torrentDetails.missingPiecesCount.ToString();
            _infoTextFields[4].Text = torrentDetails.status.ToString();
            _infoTextFields[5].Text = torrentDetails.swarmSize.ToString();
            _infoTextFields[6].Text = torrentDetails.deadPeers.ToString();
            _infoTextFields[7].Text = torrentDetails.trackerStatus.ToString();
        }
        /// <summary>
        /// Clear information window.
        /// </summary>
        public void ClearData()
        {
            Application.MainLoop.Invoke(() =>
                              {
                                  _infoTextFields[0].Text = "";
                                  _infoTextFields[1].Text = "";
                                  _infoTextFields[2].Text = "";
                                  _infoTextFields[3].Text = "";
                                  _infoTextFields[4].Text = "Idle";
                                  _infoTextFields[5].Text = "";
                                  _infoTextFields[6].Text = "";
                                  _infoTextFields[7].Text = "";
                                  _peersListView.SetSource(null);
                              });

        }
        /// <summary>
        /// Update peers in peer swarm listview.
        /// </summary>
        /// <param name="peersList"></param>
        public void UpdatePeers(string[] peersList)
        {
            _peersListView.SetSource(peersList);
        }

    }
}
