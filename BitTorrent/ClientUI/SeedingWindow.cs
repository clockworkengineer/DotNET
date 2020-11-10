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
        private readonly DemoTorrentApplication _main;
        private List<TorrentContext> _seeders;                // List of current seeding torrents
        private readonly ListView _seederListView;            // List view to displat seeding torrent information
        private DiskIO _seederDiskIO;                         // DiskIO for seeding torrents

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
        /// Update seeder listview window.
        /// </summary>
        /// <param name="main"></param>
        /// <returns></returns>
        private bool UpdateSeederList(MainLoop main)
        {

            List<string> seederLines = (from seeder in _seeders
                                        let seederDetails = _main.DownloadAgent.GetTorrentDetails(seeder)
                                        select BuildSeederDisplayLine(seederDetails)).ToList();

            if (seederLines.Count > 0)
            {
                var item = _seederListView.SelectedItem;
                _seederListView.SetSource(seederLines.ToArray());
                _seederListView.SelectedItem = item;
            }

            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="main"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public SeedingWindow(DemoTorrentApplication main, string title) : base(title)
        {
            _main = main;

            _seederListView = new ListView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true
            };

            Add(_seederListView);
        }

        /// <summary>
        /// Load torrents in the seeding list.
        /// </summary>
        public void LoadSeedingTorrents()
        {
            var token = Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);

            _seeders = new List<TorrentContext>();
            _seederDiskIO = new DiskIO();
            string[] torrentFiles = Directory.GetFiles(_main.SeedFileDirectory, "*.torrent");
            TorrentContext tc;
            Tracker seederTracker;

            foreach (var file in torrentFiles)
            {
                seederTracker = null;
                tc = null;
                try
                {
                    MetaInfoFile _seederFile = new MetaInfoFile(file);
                    _seederFile.Parse();
                    tc = new TorrentContext(_seederFile, new Selector(), _seederDiskIO, _main.DestinationTorrentDirectory, _main.SeedingMode);
                    _main.DownloadAgent.AddTorrent(tc);
                    seederTracker = new Tracker(tc);
                    _main.DownloadAgent.AttachPeerSwarmQueue(seederTracker);
                    seederTracker.StartAnnouncing();
                    _main.DownloadAgent.StartTorrent(tc);
                    _seeders.Add(tc);
                }
                catch (Exception)
                {
                    if (tc != null)
                    {
                        if (_seeders.Contains(tc))
                        {
                            _seeders.Remove(tc);
                        }
                        if (seederTracker != null)
                        {
                            seederTracker.StopAnnouncing();
                        }
                        _main.DownloadAgent.CloseTorrent(tc);
                        _main.DownloadAgent.RemoveTorrent(tc);
                    }
                    continue;
                }
            }

            Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);

        }
    }
}
