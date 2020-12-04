//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
//
// Description: Top level application class.
//
// Copyright 2020.
//
using System.IO;
using System.Threading.Tasks;
using Terminal.Gui;
using BitTorrentLibrary;
namespace ClientUI
{
    // Current application status
    public enum Status
    {
        StartingUp,     // Starting up a torrent download
        Downloading,    // Downloading torrent
        Shutdown        // Shutdown download ready for more
    };
    /// <summary>
    /// Torrent Demo Application.
    /// </summary>
    public class TorrentClient
    {
        public MainWindow MainAppicationWindow { get; set; }     // Main application window 
        public Manager TorrentManager { get; set; }              // Manager for all torrents
        public Selector TorrentSelector { get; set; }            // Selector for all torrents
        public DiskIO TorrentDiskIO { get; set; }                // DiskIO for all torrents
        public Agent TorrentAgent { get; set; }                  // Agent for handling all torrents
        public MainStatusBar MainStatusBar { get; set; }         // Main status bar
        public Config Configuration { get; set; }                // Configuration data
        // 
        /// <summary>
        /// Build application.
        /// </summary>
        public TorrentClient()
        {
            Application.Init();
            Configuration = new Config();
            Configuration.Load();
            if (!Directory.Exists(Configuration.SeedDirectory))
            {
                Directory.CreateDirectory(Configuration.SeedDirectory);
            }
            if (!Directory.Exists(Configuration.DestinationDirectory))
            {
                Directory.CreateDirectory(Configuration.DestinationDirectory);
            }
            MainAppicationWindow = new MainWindow(this, "BitTorrent Client Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            MainStatusBar = new MainStatusBar(this);
            Application.Top.Add(MainAppicationWindow, MainStatusBar);
            MainAppicationWindow.TorrentFileText.Text = Configuration.TorrentFileDirectory;
        }
        /// <summary>
        /// Startup torrent agent.
        /// </summary>
        public void TorrentAgentStartup() {
            TorrentSelector = new Selector();
            TorrentManager = new Manager();
            TorrentDiskIO = new DiskIO(TorrentManager);
            TorrentManager.AddToDeadPeerList("192.168.1.1");
            TorrentAgent = new Agent(TorrentManager, new Assembler());
            TorrentAgent.Startup();
        }
        /// <summary>
        /// Run client.
        /// </summary>
        public void Run()
        {
            TorrentAgentStartup();
            if (Configuration.SeedingTorrents)
            {
                Task.Run(() => MainAppicationWindow.SeederListWindow.LoadSeedingTorrents(TorrentAgent, TorrentSelector, TorrentDiskIO, Configuration));
            }
            Application.Run();
        }
    }
}