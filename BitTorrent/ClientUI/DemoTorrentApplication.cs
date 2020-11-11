//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Top level application.
//
// Copyright 2020.
//

using System.Threading.Tasks;
using Terminal.Gui;
using BitTorrentLibrary;

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
        public MainApplicationWindow MainWindow { get; set; }    // Main application 
        public Manager TorrentManager { get; set; }              // Torrent context manager
        public Agent DownloadAgent { get; set; }                 // Agent for handling all torrents
        public MainStatusBar MainStatusBar { get; set; }         // Main status bar
        public Config Configuration {get; set; }                 // Configuration data

        // 
        /// <summary>
        /// Build and run application.
        /// </summary>
        public DemoTorrentApplication()
        {

            Application.Init();

            Configuration = new Config();
            Configuration.Load();

            MainWindow = new MainApplicationWindow(this, "BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            MainStatusBar = new MainStatusBar(this);

            Application.Top.Add(MainWindow, MainStatusBar);

            TorrentManager = new Manager();

            TorrentManager.AddToDeadPeerList("192.168.1.1");

            DownloadAgent = new Agent(TorrentManager, new Assembler());

            DownloadAgent.Startup();

            if (Configuration.SeedingTorrents)
            {
                Task.Run(() => MainWindow.SeederListWindow.LoadSeedingTorrents(DownloadAgent, Configuration));
            }

            MainWindow.TorrentFileText.Text = Configuration.TorrentFileDirectory;

        }
        public void Run()
        {
            Application.Init();
            Application.Run();
        }
    }
}