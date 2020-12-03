//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Main status bar data and related methods.
//
// Copyright 2020.
//
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Terminal.Gui;
using BitTorrentLibrary;
namespace ClientUI
{
    public class MainStatusBar : StatusBar
    {
        private readonly List<StatusItem> _statusBarItems;  // Status bat item list
        private readonly StatusItem _download;              // Item download 
        private readonly StatusItem _shutdown;              // Item shutwdown
        private readonly StatusItem _quit;                  // Items quit
        private readonly StatusItem _toggleSeeding;         // Item toggle information/seeding sub-window
        private readonly StatusItem _toggleSeedInformation; // Item toggle seeding/seed information sub-window
        private TorrentContext _seedingInformation;         // Selected seeder torrent context
        /// <summary>
        /// Update seeder information. This is used as the tracker callback to be invoked
        /// when the next announce response is recieved back for the seeder torrent context
        /// currently selected.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateSeederInformation(Object obj)
        {
            DemoTorrentApplication main = (DemoTorrentApplication)obj;
            TorrentDetails torrentDetails = main.TorrentAgent.GetTorrentDetails(_seedingInformation);
            List<string> peers = new List<string>();
            foreach (var peer in torrentDetails.peers)
            {
                peers.Add(peer.ip + ":" + peer.port.ToString());
            }
            Application.MainLoop.Invoke(() =>
            {
                main.MainWindow.SeedingInfoWindow.UpdatePeers(peers.ToArray());
                main.MainWindow.SeedingInfoWindow.UpdateInformation(torrentDetails);
                if (torrentDetails.trackerStatus == TrackerStatus.Stalled)
                {
                    MessageBox.Query("Error", torrentDetails.trackerStatusMessage, "Ok");
                }
            });
        }
        /// <summary>
        /// Start torrent download.
        /// </summary>
        /// <param name="main"></param>
        private void ActionDownload(DemoTorrentApplication main)
        {
            main.MainWindow.Torrent = new Torrent(main.MainWindow.TorrentFileText.Text.ToString());
            Task.Run(() => main.MainWindow.Torrent.Download(main));
        }
        /// <summary>
        /// Stop currently downloading torrent.
        /// </summary>
        /// <param name="main"></param>
        private void ActionShutdown(DemoTorrentApplication main)
        {
            main.TorrentAgent.CloseTorrent(main.MainWindow.Torrent.Tc);
            main.TorrentAgent.RemoveTorrent(main.MainWindow.Torrent.Tc);
            main.MainWindow.InfoWindow.ClearData();
            main.MainStatusBar.Display(Status.Shutdown);
        }
        /// <summary>
        /// Toggle seeder list and main torrent information window.
        /// </summary>
        /// <param name="main"></param>
        private void ActionToggleSeeding(DemoTorrentApplication main)
        {
            if (main.MainWindow.DisplayInformationWindow)
            {
                main.MainWindow.Remove(main.MainWindow.InfoWindow);
                main.MainWindow.Add(main.MainWindow.SeederListWindow);
                main.MainWindow.DisplayInformationWindow = false;
                main.MainWindow.SeederListWindow.SetFocus();
                _statusBarItems.Add(_toggleSeedInformation);
            }
            else
            {
                if (!main.MainWindow.DisplaySeederInformationWindow)
                {
                    ActionToggleSeedInformation(main);
                }
                main.MainWindow.Remove(main.MainWindow.SeederListWindow);
                main.MainWindow.Add(main.MainWindow.InfoWindow);
                main.MainWindow.DisplayInformationWindow = true;
                main.MainWindow.TorrentFileText.SetFocus();
                _statusBarItems.Remove(_toggleSeedInformation);
                Items = _statusBarItems.ToArray();
            }
            Items = _statusBarItems.ToArray();
        }
        /// <summary>
        /// Toggle seeding list window and selected seeder information window.
        /// </summary>
        /// <param name="main"></param>
        private void ActionToggleSeedInformation(DemoTorrentApplication main)
        {
            if (main.MainWindow.DisplaySeederInformationWindow)
            {
                main.MainWindow.Remove(main.MainWindow.SeederListWindow);
                main.MainWindow.Add(main.MainWindow.SeedingInfoWindow);
                main.MainWindow.DisplaySeederInformationWindow = false;
                main.MainWindow.SeedingInfoWindow.SetFocus();
                List<TorrentContext> seeders = new List<TorrentContext>();
                foreach (var torrent in main.TorrentAgent.TorrentList)
                {
                    if (torrent.Status == TorrentStatus.Seeding)
                    {
                        seeders.Add(torrent);
                    }
                }
                if (seeders.Count > 0)
                {
                    _seedingInformation = main.TorrentAgent.TorrentList.ToArray()[main.MainWindow.SeederListWindow.SeederListView.SelectedItem];
                    main.MainWindow.SeedingInfoWindow.TrackerText.Text = _seedingInformation.MainTracker.TrackerURL;
                    _seedingInformation.MainTracker.SetSeedingInterval(2 * 1000);
                    _seedingInformation.MainTracker.CallBack = UpdateSeederInformation;
                    _seedingInformation.MainTracker.CallBackData = main;
                }


            }
            else
            {
                if (_seedingInformation != null)
                {
                    main.MainWindow.SeedingInfoWindow.ClearData();
                    _seedingInformation.MainTracker.SetSeedingInterval(60000 * 30);
                    _seedingInformation.MainTracker.CallBack = null;
                    _seedingInformation.MainTracker.CallBack = null;
                    main.MainWindow.Remove(main.MainWindow.SeedingInfoWindow);
                    _seedingInformation = null;
                }
                main.MainWindow.Add(main.MainWindow.SeederListWindow);
                main.MainWindow.DisplaySeederInformationWindow = true;
                main.MainWindow.SeederListWindow.SetFocus();
            }
        }
        /// <summary>
        /// Quit application.
        /// </summary>
        /// <param name="main"></param>
        private void ActionQuit(DemoTorrentApplication main)
        {
            main.TorrentAgent.ShutDown();
            Application.Top.Running = false;
        }
        /// <summary>
        /// Intialise main application status bar
        /// </summary>
        /// <param name="main"></param>
        public MainStatusBar(DemoTorrentApplication main)
        {
            _statusBarItems = new List<StatusItem>();
            _download = new StatusItem(Key.ControlD, "~^D~ Download", () =>
            {
                ActionDownload(main);
            });
            _shutdown = new StatusItem(Key.ControlS, "~^S~ shutdown", () =>
            {
                ActionShutdown(main);
            });
            _toggleSeeding = new StatusItem(Key.ControlT, "~^T~ Toggle Seeding", () =>
            {
                ActionToggleSeeding(main);
            });
            _toggleSeedInformation = new StatusItem(Key.ControlJ, "~^J~ Seed Information", () =>
            {
                ActionToggleSeedInformation(main);
            });
            _quit = new StatusItem(Key.ControlQ, "~^Q~ Quit", () =>
            {
                ActionQuit(main);
            });
            _statusBarItems.Add(_download);
            _statusBarItems.Add(_toggleSeeding);
            _statusBarItems.Add(_quit);
            Items = _statusBarItems.ToArray();
        }
        /// <summary>
        /// Display program status bar.
        /// </summary>
        /// <param name="status"></param>
        public void Display(Status status)
        {
            switch (status)
            {
                case Status.Starting:
                    _statusBarItems.Clear();
                    _statusBarItems.Add(_quit);
                    break;
                case Status.Downloading:
                    _statusBarItems.Clear();
                    _statusBarItems.Add(_toggleSeeding);
                    _statusBarItems.Add(_shutdown);
                    _statusBarItems.Add(_quit); ;
                    break;
                case Status.Shutdown:
                    _statusBarItems.Clear();
                    _statusBarItems.Add(_toggleSeeding);
                    _statusBarItems.Add(_download);
                    _statusBarItems.Add(_quit);
                    break;
            }
            Items = _statusBarItems.ToArray();
        }
    }
}
