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

        private readonly List<StatusItem> _statusBarItems;  // Status bat item list
        private readonly StatusItem _download;              // Item download 
        private readonly StatusItem _shutdown;              // Item shutwdown
        private readonly StatusItem _quit;                  // Items quit
        private readonly StatusItem _toggleSeeding;         // Item toggle information/seeding sub-windows
        private StatusBar _mainStatusBar;                   // Main status bar
        private readonly Toplevel _top;                     // Top level application view
        private bool informatioWindow = true;               // == true then information window displayed otherwise seeding
        private List<TorrentContext> _seeders;              // List of current seeding torrents
        private ListView _seederListView;                   // List view to displat seeding torrent information
        private DiskIO _seederDiskIO;                       // DiskIO for seeding torrents
        public MainWindow MainWindow { get; set; }          // Main application 
        public Manager TorrentManager { get; set; }           // Torrent context manager
        public Agent DownloadAgent { get; set; }            // Agent for handling all torrents

        // Cofig values
        public string SeedFileDirectory { get; set; } = "";         // Directory containign torrent files that are seeding
        public string DestinationTorrentDirectory { get; set; } = "";      // Destination for torrents downloaded
        public string TorrentFileDirectory { get; set; } = "";    // Default path for torrent field field
        public bool SeedingMode { get; set; } = true;               // == true dont check torrents disk inage on startup
        public bool SeedingTorrents { get; set; } = true;          // == true load seeding torrents

        /// <summary>
        /// Read config settings
        /// </summary>
        public void ReadConfig()
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
                   seederDetails.trackerStatus.ToString().Substring(0,3), 
                   seederDetails.status.ToString().Substring(0,1),
                   seederDetails.uploadedBytes, seederDetails.swarmSize);

        }
        /// <summary>
        /// Update seeder listview window.
        /// </summary>
        /// <param name="main"></param>
        /// <returns></returns>
        public bool UpdateSeederList(MainLoop main)
        {
            List<string> seederLines = new List<string>();

            foreach (var seeder in _seeders)
            {
                TorrentDetails seederDetails = DownloadAgent.GetTorrentDetails(seeder);
                seederLines.Add(BuildSeederDisplayLine(seederDetails));
            }
            if (_seederListView != null)
            {
                MainWindow.SeedingWindow.Remove(_seederListView);
            }
            _seederListView = new ListView(seederLines.ToArray())
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = false
            };
            MainWindow.SeedingWindow.Add(_seederListView);

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

                    _seederFile.Load();
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
                        if (_seeders.Contains(tc)) {
                            _seeders.Remove(tc);
                        }
                        if (seederTracker != null)
                        {
                            seederTracker.StopAnnouncing();
                        }
                        DownloadAgent.Close(tc);
                        DownloadAgent.RemoveTorrent(tc);
                    }
                    continue;
                }


            }

            Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);

        }
        /// <summary>
        /// Display program status bar.
        /// </summary>
        /// <param name="status"></param>
        public void DisplayStatusBar(Status status)
        {

            _top.Remove(_mainStatusBar);
            if (status == Status.Starting)
            {
                _statusBarItems.Clear();
                _statusBarItems.Add(_quit);
                _mainStatusBar = new StatusBar(_statusBarItems.ToArray());
                _top.Add(_mainStatusBar);
            }
            else if (status == Status.Downloading)
            {
                _statusBarItems.Clear();
                _statusBarItems.Add(_toggleSeeding);
                _statusBarItems.Add(_shutdown);
                _statusBarItems.Add(_quit);
                _mainStatusBar = new StatusBar(_statusBarItems.ToArray());
                _top.Add(_mainStatusBar);
            }
            else if (status == Status.Shutdown)
            {
                _statusBarItems.Clear();
                _statusBarItems.Add(_toggleSeeding);
                _statusBarItems.Add(_download);
                _statusBarItems.Add(_quit);
                _mainStatusBar = new StatusBar(_statusBarItems.ToArray());
                _top.Add(_mainStatusBar);
            }
        }
        /// <summary>
        /// Build and run application.
        /// </summary>
        public DemoTorrentApplication()
        {

            Application.Init();
            _top = Application.Top;

            ReadConfig();

            MainWindow = new MainWindow("BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _statusBarItems = new List<StatusItem>();

            _download = new StatusItem(Key.ControlD, "~^D~ Download", () =>
            {
                MainWindow.Torrent = new Torrent(MainWindow.TorrentFileText.Text.ToString());
                MainWindow.DownloadTorrentTask = Task.Run(() => MainWindow.Torrent.Download(this));
            });

            _shutdown = new StatusItem(Key.ControlS, "~^S~ shutdown", () =>
             {
                 DownloadAgent.RemoveTorrent(MainWindow.Torrent.Tc);
                 DownloadAgent.Close(MainWindow.Torrent.Tc);
                 MainWindow.InformationWindow.ClearData();
                 DisplayStatusBar(Status.Shutdown);
             });

            _toggleSeeding = new StatusItem(Key.ControlT, "~^T~ Toggle Seeding", () =>
            {
                if (informatioWindow)
                {
                    MainWindow.Remove(MainWindow.InformationWindow);
                    MainWindow.Add(MainWindow.SeedingWindow);
                    informatioWindow = false;
                }
                else
                {
                    MainWindow.Remove(MainWindow.SeedingWindow);
                    MainWindow.Add(MainWindow.InformationWindow);
                    informatioWindow = true;
                }
            });

            _quit = new StatusItem(Key.ControlQ, "~^Q~ Quit", () =>
            {
                DownloadAgent.ShutDown();
                _top.Running = false;
            });

            _statusBarItems.Add(_download);
            _statusBarItems.Add(_toggleSeeding);
            _statusBarItems.Add(_quit);

            _mainStatusBar = new StatusBar(_statusBarItems.ToArray());

            _top.Add(MainWindow, _mainStatusBar);

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