//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
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
        private Agent _agent;                            // Main torrent agent
        public ListView SeederListView { get; }          // List view to displat seeding torrent information

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
            List<string> seederLines = (from seeder in _agent.TorrentList
                                        let seederDetails = _agent.GetTorrentDetails(seeder)
                                        where seederDetails.status == TorrentStatus.Seeding
                                        select BuildSeederDisplayLine(seederDetails)).ToList();
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
        /// Load torrents in the seeding list.
        /// </summary>
        public void LoadSeedingTorrents(Agent agent, Selector selector, DiskIO diskIO, Config config)
        {
            TorrentContext tc = null;
            Tracker seederTracker = null;
            _agent = agent;
            Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);
            foreach (var file in Directory.GetFiles(config.SeedDirectory, "*.torrent"))
            {
                try
                {
                    MetaInfoFile seederFile = new MetaInfoFile(file);
                    seederFile.Parse();
                    tc = new TorrentContext(seederFile, selector, diskIO, config.DestinationDirectory, config.SeedingMode);
                    seederTracker = new Tracker(tc);
                    agent.AddTorrent(tc);
                    agent.AttachPeerSwarmQueue(seederTracker);
                    seederTracker.StartAnnouncing();
                    agent.StartTorrent(tc);
                }
                catch (Exception)
                {
                    if (tc != null)
                    {
                        agent.CloseTorrent(tc);
                        agent.RemoveTorrent(tc);
                        tc = null;
                    }
                    continue;
                }
            }
        }
    }
}
