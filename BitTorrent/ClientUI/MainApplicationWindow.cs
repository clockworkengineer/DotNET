//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Class that defines the layout of the applications main window.
//
// Copyright 2020.
//
using System.Collections.Generic;
using System.Threading.Tasks;
using Terminal.Gui;
namespace ClientUI
{
    /// <summary>
    /// Main Application window.
    /// </summary>
    public class MainApplicationWindow : Window
    {
        private readonly Label torrentFileLabel;                    // Torrent file field label
        private readonly Label _progressBarBeginText;               // Beginning of progress bar '['
        private readonly Label _progressBarEndText;                 // End of progress bar ']'
        public TextField TorrentFileText { get; set; }              // Text field containing torrent file name
        public ProgressBar DownloadProgress { get; set; }           // Downloading progress bar
        public InformationWindow InfoWindow { get; set; }           // Torrent information sub-window
        public InformationWindow SeedingInfoWindow { get; set; }    // Torrent information sub-window
        public SeedingWindow SeederListWindow { get; set; }         // Seeding torrents sub-window (overlays information)
        public Torrent Torrent { get; set; }                        // Currently active downloading torrent
        public bool DisplayInformationWindow { get; set; } = true;  // == true information window displayed
        public bool DisplaySeederInformationWindow { get; set; } = true;  // == true seeder information window displayed
        /// <summary>
        /// Build main application window including the information
        /// and seeding windows which overlay each other depending which
        /// is toggled to display.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public MainApplicationWindow(DemoTorrentApplication _, string name) : base(name)
        {
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
            SeedingInfoWindow = new InformationWindow("Seed Info")
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
    }
}
