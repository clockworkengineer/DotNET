using System.Net.NetworkInformation;
using System.Net;
using System.Threading.Tasks.Dataflow;
using System.Net.Mail;
using System.Collections.ObjectModel;
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
        private List<StatusItem> _statusBarItems = new List<StatusItem>();
        private StatusItem _download;
        private StatusItem _shutdown;
        private StatusItem _quit;
        private StatusItem _toggleSeeding;
        private StatusBar _mainStatusBar;
        private Toplevel _top;
        private bool informatioWindow = true;
        public MainWindow MainWindow { get; set; }

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
                 MainWindow.Torrent.DownloadAgent.Close(MainWindow.Torrent.Tc);
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

            _quit = new StatusItem(Key.ControlQ, "~^Q~ Quit", () => { _top.Running = false; });
            _statusBarItems.Add(_download);
            _statusBarItems.Add(_quit);

            _mainStatusBar = new StatusBar(_statusBarItems.ToArray());

            _top.Add(MainWindow, _mainStatusBar);
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
