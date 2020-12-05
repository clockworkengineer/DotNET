//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
//
// Description: Main status bar data and related methods.
//
// Copyright 2020.
//
using System.Threading.Tasks;
using System.Collections.Generic;
using Terminal.Gui;
namespace ClientUI
{
    public class MainStatusBar : StatusBar
    {
        private readonly List<StatusItem> _statusBarItems;  // Status bat item list
        private readonly StatusItem _download;              // Item download 
        private readonly StatusItem _shutdown;              // Item shutdown
        private readonly StatusItem _quit;                  // Items quit
        private readonly StatusItem _toggleSeeding;         // Item toggle information/seeding sub-window
        private readonly StatusItem _toggleSeedInformation; // Item toggle seeding/seed information sub-window
        /// <summary>
        /// Toggle seeding status bar item
        /// </summary>
        /// <param name="main"></param>
        private void ToggleSeeding(bool on)
        {
            if (on)
            {
                _statusBarItems.Add(_toggleSeedInformation);
            }
            else
            {
                _statusBarItems.Remove(_toggleSeedInformation);
            }
            Items = _statusBarItems.ToArray();
        }
        /// <summary>
        /// Start torrent download.
        /// </summary>
        /// <param name="main"></param>
        private void ActionDownload(TorrentClient main)
        {
            main.ClientWindow.TorrentHandler.SetDownloadTorrent(main.ClientWindow.TorrentFileText.Text.ToString());
            Task.Run(() => main.ClientWindow.TorrentHandler.Download(main));
        }
        /// <summary>
        /// Stop currently downloading torrent.
        /// </summary>
        /// <param name="main"></param>
        private void ActionShutdown(TorrentClient main)
        {
            main.ClientWindow.ClosedownTorrent();
            main.MainStatusBar.Display(Status.Shutdown);
        }
        /// <summary>
        /// Toggle seeder list and main torrent information window.
        /// </summary>
        /// <param name="main"></param>
        private void ActionToggleSeeding(TorrentClient main)
        {
            ToggleSeeding(main.ClientWindow.DisplayInformationWindow);
            main.ClientWindow.ToggleSeedingList();
        }
        /// <summary>
        /// Toggle seeding list window and selected seeder information window.
        /// </summary>
        /// <param name="main"></param>
        private void ActionToggleSeedInformation(TorrentClient main)
        {
            main.ClientWindow.ToggleSeedinginformation();
        }
        /// <summary>
        /// Quit application.
        /// </summary>
        /// <param name="main"></param>
        private void ActionQuit(TorrentClient main)
        {
            main.ClientWindow.TorrentHandler.Shutdown();
            Application.Top.Running = false;
        }
        /// <summary>
        /// Intialise main application status bar
        /// </summary>
        /// <param name="main"></param>
        public MainStatusBar(TorrentClient main)
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
            Display(Status.Shutdown);
        }
        /// <summary>
        /// Display program status bar.
        /// </summary>
        /// <param name="status"></param>
        public void Display(Status status)
        {
            switch (status)
            {
                case Status.StartingUp:
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
