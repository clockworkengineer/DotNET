//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Top level application object.
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terminal.Gui;
using System.IO;
using BitTorrentLibrary;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace ClientUI
{
    // Current application status
    public enum Status
    {
        Starting,
        Downloading,
        Shutdown
    };

    /// <summary>
    /// Torrent Demo Application.
    /// </summary>
    public class DemoTorrentApplication
    {

        private List<TorrentContext> _seeders;                // List of current seeding torrents
        private readonly ListView _seederListView;            // List view to displat seeding torrent information
        private DiskIO _seederDiskIO;                         // DiskIO for seeding torrents
        public bool InformationWindow { get; set; } = true;   // == true information window displayed
        public MainApplicationWindow MainWindow { get; set; } // Main application 
        public Manager TorrentManager { get; set; }           // Torrent context manager
        public Agent DownloadAgent { get; set; }              // Agent for handling all torrents
        public MainStatusBar MainStatusBar { get; set; }      // Mains status bar
        public Toplevel Top { get; set; }                     // Top level application view

        // Cofig values
        public string SeedFileDirectory { get; set; } = "";             // Directory containign torrent files that are seeding
        public string DestinationTorrentDirectory { get; set; } = "";   // Destination for torrents downloaded
        public string TorrentFileDirectory { get; set; } = "";          // Default path for torrent field field
        public bool SeedingMode { get; set; } = true;                   // == true dont check torrents disk inage on startup
        public bool SeedingTorrents { get; set; } = true;               // == true load seeding torrents


        /// <summary>
        /// Read config settings
        /// </summary>
        private void ReadConfig()
        {
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                  .AddJsonFile("appsettings.json", true, true)
                  .Build();

                TorrentFileDirectory = config["TorrentFileDirectory"];
                DestinationTorrentDirectory = config["DestinationTorrentDirectory"];
                SeedFileDirectory = config["SeedFileDirectory"];
                SeedingMode = bool.Parse(config["SeedingMode"]);
                SeedingTorrents = bool.Parse(config["LoadSeedingTorrents"]);

            }
            catch (Exception ex)
            {
                Log.Logger.Debug("Application Error : " + ex.Message);
            }

        }
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
                                        let seederDetails = DownloadAgent.GetTorrentDetails(seeder)
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
        /// Load torrents in the seeding list.
        /// </summary>
        private void LoadSeedingTorrents()
        {
            var token = Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);

            _seeders = new List<TorrentContext>();
            _seederDiskIO = new DiskIO();
            string[] torrentFiles = Directory.GetFiles(SeedFileDirectory, "*.torrent");
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
                    tc = new TorrentContext(_seederFile, new Selector(), _seederDiskIO, DestinationTorrentDirectory, SeedingMode);
                    DownloadAgent.AddTorrent(tc);
                    seederTracker = new Tracker(tc);
                    DownloadAgent.AttachPeerSwarmQueue(seederTracker);
                    seederTracker.StartAnnouncing();
                    DownloadAgent.StartTorrent(tc);
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
                        DownloadAgent.CloseTorrent(tc);
                        DownloadAgent.RemoveTorrent(tc);
                    }
                    continue;
                }
            }

            Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);

        }
        /// <summary>
        /// Build and run application.
        /// </summary>
        public DemoTorrentApplication()
        {

            Application.Init();
            Top = Application.Top;

            ReadConfig();

            MainWindow = new MainApplicationWindow("BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _seederListView = new ListView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true
            };
            
            MainWindow.SeedingWindow.Add(_seederListView);

            MainStatusBar = new MainStatusBar(this);

            Top.Add(MainWindow, MainStatusBar);

            TorrentManager = new Manager();

            TorrentManager.AddToDeadPeerList("192.168.1.1");

            DownloadAgent = new Agent(TorrentManager, new Assembler());

            DownloadAgent.Startup();

            if (SeedingTorrents)
            {
                Task.Run(() => LoadSeedingTorrents());
            }

            MainWindow.TorrentFileText.Text = TorrentFileDirectory;

        }
        public void Run()
        {
            Application.Init();
            Application.Run();
        }
    }
}