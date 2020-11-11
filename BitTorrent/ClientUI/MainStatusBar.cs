//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
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
        private readonly StatusItem _shutdown;              // Item shutwdown
        private readonly StatusItem _quit;                  // Items quit
        private readonly StatusItem _toggleSeeding;         // Item toggle information/seeding sub-window

        /// <summary>
        /// 
        /// </summary>
        /// <param name="main"></param>
        public MainStatusBar(DemoTorrentApplication main)
        {
            _statusBarItems = new List<StatusItem>();

            _download = new StatusItem(Key.ControlD, "~^D~ Download", () =>
            {
                main.MainWindow.Torrent = new Torrent(main.MainWindow.TorrentFileText.Text.ToString());
                Task.Run(() => main.MainWindow.Torrent.Download(main));
            });

            _shutdown = new StatusItem(Key.ControlS, "~^S~ shutdown", () =>
            {
                main.DownloadAgent.RemoveTorrent(main.MainWindow.Torrent.Tc);
                main.DownloadAgent.CloseTorrent(main.MainWindow.Torrent.Tc);
                main.MainWindow.InfoWindow.ClearData();
                main.MainStatusBar.Display(Status.Shutdown);
            });

            _toggleSeeding = new StatusItem(Key.ControlT, "~^T~ Toggle Seeding", () =>
            {
                if (main.MainWindow.DisplayInformationWindow)
                {
                    main.MainWindow.Remove(main.MainWindow.InfoWindow);
                    main.MainWindow.Add(main.MainWindow.SeederListWindow);
                    main.MainWindow.DisplayInformationWindow = false;
                    main.MainWindow.SeederListWindow.SetFocus();
                }
                else
                {
                    main.MainWindow.Remove(main.MainWindow.SeederListWindow);
                    main.MainWindow.Add(main.MainWindow.InfoWindow);
                    main.MainWindow.DisplayInformationWindow = true;
                    main.MainWindow.TorrentFileText.SetFocus();
                }
            });

            _quit = new StatusItem(Key.ControlQ, "~^Q~ Quit", () =>
            {
                main.DownloadAgent.ShutDown();
                Application.Top.Running = false;
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

            if (status == Status.Starting)
            {
                _statusBarItems.Clear();
                _statusBarItems.Add(_quit);
                Items = _statusBarItems.ToArray();
            }
            else if (status == Status.Downloading)
            {
                _statusBarItems.Clear();
                _statusBarItems.Add(_toggleSeeding);
                _statusBarItems.Add(_shutdown);
                _statusBarItems.Add(_quit); ;
            }
            else if (status == Status.Shutdown)
            {
                _statusBarItems.Clear();
                _statusBarItems.Add(_toggleSeeding);
                _statusBarItems.Add(_download);
                _statusBarItems.Add(_quit);

            }
            Items = _statusBarItems.ToArray();
        }
    }


}
