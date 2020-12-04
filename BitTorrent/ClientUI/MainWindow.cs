//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
//
// Description: Class that defines the layout of the applications main window.
//
// Copyright 2020.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Terminal.Gui;
using BitTorrentLibrary;
namespace ClientUI
{
    /// <summary>
    /// Main Application window.
    /// </summary>
    public class MainWindow : Window
    {
        private readonly TorrentClient _main;                       // Main torrent client
        private readonly Label torrentFileLabel;                    // Torrent file field label
        private readonly Label _progressBarBeginText;               // Beginning of progress bar '['
        private readonly Label _progressBarEndText;                 // End of progress bar ']'
        private TorrentContext _seedingInformation;                 // Selected seeder torrent context
        private bool _displaySeederInformationWindow = true;        // == true seeder information window 
        public InformationWindow _seedingInfoWindow;                // Seeding torrent information sub-window
        public TextField TorrentFileText { get; set; }              // Text field containing torrent file name
        public ProgressBar DownloadProgress { get; set; }           // Downloading progress bar
        public InformationWindow InfoWindow { get; set; }           // Torrent information sub-window
        public SeedingWindow SeederListWindow { get; set; }         // Seeding torrents sub-window (overlays information)
        public Torrent Torrent { get; set; }                        // Currently active downloading torrent
        public bool DisplayInformationWindow { get; set; } = true;  // == true information window displayed

        /// <summary>
        /// Update seeder information. This is used as the tracker callback to be invoked
        /// when the next announce response is recieved back for the seeder torrent context
        /// currently selected.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateSeederInformation(Object obj)
        {
            TorrentClient main = (TorrentClient)obj;
            TorrentDetails torrentDetails = main.TorrentAgent.GetTorrentDetails(_seedingInformation);
            List<string> peers = new List<string>();
            foreach (var peer in torrentDetails.peers)
            {
                peers.Add(peer.ip + ":" + peer.port.ToString());
            }
            Application.MainLoop.Invoke(() =>
            {
                _seedingInfoWindow.UpdatePeers(peers.ToArray());
                _seedingInfoWindow.UpdateInformation(torrentDetails);
                if (torrentDetails.trackerStatus == TrackerStatus.Stalled)
                {
                    MessageBox.Query("Error", torrentDetails.trackerStatusMessage, "Ok");
                }
            });
        }
        /// <summary>
        /// Build main application window including the information
        /// and seeding windows which overlay each other depending which
        /// is toggled to display.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public MainWindow(TorrentClient main, string name) : base(name)
        {
            _main = main;
            List<View> viewables = new List<View>();
            torrentFileLabel = new Label("Torrent File: ")
            {
                X = 1,
                Y = 1
            };
            viewables.Add(torrentFileLabel);
            TorrentFileText = new TextField()
            {
                X = Pos.Right(torrentFileLabel),
                Y = Pos.Top(torrentFileLabel),
                Width = 50,
            };
            viewables.Add(TorrentFileText);
            TorrentFileText.CursorPosition = TorrentFileText.ToString().Length;
            _progressBarBeginText = new Label("Progress : [")
            {
                X = Pos.Left(torrentFileLabel),
                Y = Pos.Bottom(torrentFileLabel) + 1,
            };
            viewables.Add(_progressBarBeginText);
            DownloadProgress = new ProgressBar()
            {
                X = Pos.Right(_progressBarBeginText),
                Y = Pos.Bottom(torrentFileLabel) + 1,
                Width = 60,
                Height = 1
            };
            viewables.Add(DownloadProgress);
            _progressBarEndText = new Label("]")
            {
                X = Pos.Right(DownloadProgress) - 1,
                Y = Pos.Bottom(torrentFileLabel) + 1,
            };
            viewables.Add(_progressBarEndText);
            InfoWindow = new InformationWindow("Information")
            {
                X = Pos.Left(this),
                Y = Pos.Bottom(_progressBarBeginText) + 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = false
            };
            viewables.Add(InfoWindow);
            SeederListWindow = new SeedingWindow("Seeding")
            {
                X = Pos.Left(this),
                Y = Pos.Bottom(_progressBarBeginText) + 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = false
            };
            _seedingInfoWindow = new InformationWindow("Seed Info")
            {
                X = Pos.Left(this),
                Y = Pos.Bottom(_progressBarBeginText) + 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = false
            };
            foreach (var viewable in viewables)
            {
                Add(viewable);
            }
        }
        /// <summary>
        /// Close down main torrent
        /// </summary>
        /// <param name="_main"></param>
        public void CloseDownTorrent()
        {
            _main.TorrentAgent.CloseTorrent(Torrent.Tc);
            _main.TorrentAgent.RemoveTorrent(Torrent.Tc);
            InfoWindow.ClearData();
        }
        /// <summary>
        /// Toggle seeding list and torrent information window
        /// </summary>
        public void ToggleSeedingList()
        {
            if (DisplayInformationWindow)
            {
                Remove(InfoWindow);
                Add(SeederListWindow);
                DisplayInformationWindow = false;
                SeederListWindow.SetFocus();
            }
            else
            {
                if (!_displaySeederInformationWindow)
                {
                    ToggleSeedinginformation();
                }
                Remove(SeederListWindow);
                Add(InfoWindow);
                DisplayInformationWindow = true;
                TorrentFileText.SetFocus();
            }
        }
        /// <summary>
        /// Toggle seeding list and seeder information window
        /// </summary>
        /// <param name="_main"></param>
        public void ToggleSeedinginformation()
        {
            if (_displaySeederInformationWindow)
            {
                Remove(SeederListWindow);
                Add(_seedingInfoWindow);
                _displaySeederInformationWindow = false;
                List<TorrentContext> seeders = new List<TorrentContext>();
                foreach (var torrent in _main.TorrentAgent.TorrentList)
                {
                    if (torrent.Status == TorrentStatus.Seeding)
                    {
                        seeders.Add(torrent);
                    }
                }
                if (seeders.Count > 0)
                {
                    _seedingInformation = _main.TorrentAgent.TorrentList.ToArray()[SeederListWindow.SeederListView.SelectedItem];
                    _seedingInfoWindow.TrackerText.Text = _seedingInformation.MainTracker.TrackerURL;
                    _seedingInformation.MainTracker.SetSeedingInterval(2 * 1000);
                    _seedingInformation.MainTracker.CallBack = UpdateSeederInformation;
                    _seedingInformation.MainTracker.CallBackData = _main;
                    _seedingInfoWindow.SetFocus();
                }
            }
            else
            {
                if (_seedingInformation != null)
                {
                    _seedingInfoWindow.ClearData();
                    _seedingInformation.MainTracker.SetSeedingInterval(60000 * 30);
                    _seedingInformation.MainTracker.CallBack = null;
                    _seedingInformation.MainTracker.CallBack = null;
                    Remove(_seedingInfoWindow);
                    _seedingInformation = null;
                }
                Add(SeederListWindow);
                _displaySeederInformationWindow = true;
                SeederListWindow.SetFocus();
            }
        }
    }
}
