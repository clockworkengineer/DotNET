//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
//
// Description: Seeders list window data and related methods.
//
// Copyright 2020.
//
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BitTorrentLibrary;
using Terminal.Gui;
namespace ClientUI
{
    public class SeedingWindow : Window
    {
        private Torrent _torrent;                        // Main torrent
        public ListView SeederListView { get; }          // List view to display seeding torrent information

        /// <summary>
        /// Build seeder display line for listview.
        /// </summary>
        /// <param name="seederDetails"></param>
        /// <returns></returns>
        private string BuildSeederDisplayLine(TorrentDetails seederDetails)
        {
            return String.Format("File[{0,-12}] Tracker[{1,3}] Status[{2,1}] Uploaded[{3, 11}] Swarm[{4, 5}]",
                   Path.GetFileNameWithoutExtension(seederDetails.fileName),
                   seederDetails.trackerStatus.ToString().Substring(0, 3),
                   seederDetails.status.ToString().Substring(0, 1),
                   seederDetails.uploadedBytes, seederDetails.swarmSize);
        }
        /// <summary>
        /// Update seeder list window.
        /// </summary>
        /// <param name="main"></param>
        /// <returns></returns> 
        private bool UpdateSeederList(MainLoop main)
        {
            List<TorrentDetails> seeders = _torrent.GetSeedingTorrentDetails();
            List<string> seederLines = (from seeder in seeders
                                        select BuildSeederDisplayLine(seeder)).ToList();
            if (seederLines.Count > 0)
            {
                var item = SeederListView.SelectedItem;
                SeederListView.SetSource(seederLines.ToArray());
                SeederListView.SelectedItem = item;
            }
            return true;
        }
        /// <summary>
        /// Initialise seeder list view window
        /// </summary>
        /// <param name="main"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public SeedingWindow(string title) : base(title)
        {
            SeederListView = new ListView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true
            };
            Add(SeederListView);
        }
        /// <summary>
        /// Load torrents in the seeding directory.
        /// </summary>
        public void LoadSeedingTorrents(TorrentClient main)
        {
            _torrent = main.ClientWindow.MainTorrent;
            foreach (var file in Directory.GetFiles(main.Configuration.SeedDirectory, "*.torrent"))
            {
                main.ClientWindow.MainTorrent.AddSeedingTorrent(file, main.Configuration);
            }
            Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);
        }
    }
}
