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
        StartingUp,
        Downloading,
        Shutdown
    };
    /// <summary>
    /// Torrent Demo Application.
    /// </summary>
    public class DemoTorrentApplication
    {
        public MainApplicationWindow MainWindow { get; set; }    // Main application 
        public Manager TorrentManager { get; set; }              // Manager for all torrents
        public Selector TorrentSelector { get; set; }            // Selector for all torrents
        public DiskIO TorrentDiskIO { get; set; }                // DiskIO for all torrents
        public Agent TorrentAgent { get; set; }                  // Agent for handling all torrents
        public MainStatusBar MainStatusBar { get; set; }         // Main status bar
        public Config Configuration { get; set; }                // Configuration data
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
            TorrentSelector = new Selector();
            TorrentManager = new Manager();
            TorrentDiskIO = new DiskIO(TorrentManager);
            TorrentManager.AddToDeadPeerList("192.168.1.1");
            TorrentAgent = new Agent(TorrentManager, new Assembler());
            TorrentAgent.Startup();
            if (Configuration.SeedingTorrents)
            {
                Task.Run(() => MainWindow.SeederListWindow.LoadSeedingTorrents(TorrentAgent, TorrentSelector, TorrentDiskIO, Configuration));
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