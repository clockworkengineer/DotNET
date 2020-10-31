//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: 
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terminal.Gui;
using System.Text;
using System.IO;
using BitTorrentLibrary;

namespace ClientUI
{

    public enum Status
    {
        Starting,
        Downloading,
        Shutdown
    };

    /// <summary>
    /// 
    /// </summary>
    public class DemoTorrentApplication
    {
        private readonly List<StatusItem> _statusBarItems = new List<StatusItem>();
        private readonly StatusItem _download;
        private readonly StatusItem _shutdown;
        private readonly StatusItem _quit;
        private readonly StatusItem _toggleSeeding;
        private StatusBar _mainStatusBar;
        private readonly Toplevel _top;
        private bool informatioWindow = true;
        private List<TorrentContext> _seeders;
        private ListView _seederListView;
        private Downloader _seederDownloader;
        public MainWindow MainWindow { get; set; }
        public Agent DownloadAgent { get; set; }
        public string SeedFileDirectory { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        public static string InfoHashToString(byte[] infoHash)
        {
            StringBuilder hex = new StringBuilder(infoHash.Length * 2);
            foreach (byte b in infoHash)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString().ToLower();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="main"></param>
        /// <returns></returns>
        public bool UpdateSeederList(MainLoop main)
        {
            List<string> seederLines = new List<string>();

            foreach (var seeder in _seeders)
            {
                TorrentDetails seederDetails = DownloadAgent.GetTorrentDetails(seeder);
                seederLines.Add($"File[{Path.GetFileName(seederDetails.fileName)}] Status[{seederDetails.status}] Uploaded[{seederDetails.uploadedBytes}]");
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
        /// 
        /// </summary>
        private void LoadSeedingTorrents()
        {
            var token = Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);

            _seeders = new List<TorrentContext>();
            _seederDownloader = new Downloader();
            string[] torrentFiles = Directory.GetFiles(SeedFileDirectory, "*.torrent");
            foreach (var file in torrentFiles)
            {

                MetaInfoFile _seederFile = new MetaInfoFile(file);
                _seederFile.Load();
                _seederFile.Parse();

                TorrentContext tc = new TorrentContext(_seederFile, new Selector(), _seederDownloader, "/home/robt/utorrent");

                DownloadAgent.Add(tc);

                Tracker seederTracker = new Tracker(tc);

                seederTracker.SetPeerSwarmQueue(DownloadAgent.PeerSwarmQueue);

                seederTracker.StartAnnouncing();

                DownloadAgent.Start(tc);

                _seeders.Add(tc);

                DownloadAgent.Download(tc);

                 Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), UpdateSeederList);

            }
        }
        /// <summary>
        /// 
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
        /// 
        /// </summary>
        public DemoTorrentApplication()
        {
            Application.Init();
            _top = Application.Top;

            SeedFileDirectory = "/home/robt/Projects/dotNET/BitTorrent/ClientUI/bin/Debug/netcoreapp3.1/seeding/";

            MainWindow = new MainWindow("BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _download = new StatusItem(Key.ControlD, "~^D~ Download", () =>
            {
                MainWindow.Torrent = new Torrent(MainWindow.TorrentFileText.Text.ToString());
                MainWindow.DownloadTorrentTask = Task.Run(() => MainWindow.Torrent.Download(this));
            });

            _shutdown = new StatusItem(Key.ControlS, "~^S~ shutdown", () =>
             {
                 DownloadAgent.Remove(MainWindow.Torrent.Tc);
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

            DownloadAgent = new Agent(new Assembler());

            Task.Run(() => LoadSeedingTorrents());

        }
        /// <summary>
        /// 
        /// </summary>
        void Run()
        {
            Application.Init();
            Application.Run();
        }

        /// <summary>
        /// 
        /// </summary>
        static void Main()
        {

            DemoTorrentApplication main = new DemoTorrentApplication();

            main.Run();

        }
    }
}
