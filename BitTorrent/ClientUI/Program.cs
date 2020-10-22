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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitTorrentLibrary;
using Terminal.Gui;

public class Torrent
{
    private readonly string _torrentFileName;
    private MetaInfoFile _torrentFile;
    private Downloader _downloader;
    private Selector _selector;
    private Assembler _assembler;
    private Agent _agent;
    private ProgressBar _progressBar;
    private double _currentProgress = 0;
    private bool downloading = false;
    Tracker tracker;
    public Torrent(string torrentFileName)
    {
        _torrentFileName = torrentFileName;
    }

    public void UpdateProgress(Object obj)
    {
        Torrent torrent = (Torrent)obj;
        double progress = (double)_downloader.Dc.TotalBytesDownloaded /
        (double)_downloader.Dc.TotalBytesToDownload;
        if (progress - _currentProgress > 0.05)
        {
            Application.MainLoop.Invoke(() =>
            {
                torrent._progressBar.Fraction = (float)progress;
            });
            torrent._progressBar.Fraction = (float)progress;
            _currentProgress = progress;
        }
    }
    public void Download(ProgressBar progressBar)
    {
        try
        {
            _progressBar = progressBar;

            _torrentFile = new MetaInfoFile(_torrentFileName);

            _torrentFile.Load();
            _torrentFile.Parse();

            _downloader = new Downloader(_torrentFile, "/home/robt/utorrent");
            _selector = new Selector(_downloader.Dc);
            _assembler = new Assembler(_downloader, this.UpdateProgress, this);
            _agent = new Agent(_torrentFile, _downloader, _assembler);

            tracker = new Tracker(_agent, _downloader);

            tracker.StartAnnouncing();

            _agent.Start();

            downloading = true;

            _agent.Download();
            
            downloading = false;
        }
        catch (Exception ex)
        {

        }
    }
}
public class TorrentFileNameText : TextField
{
    public Torrent Torrent { get; set; }

    public override bool OnLeave(View view)
    {
        Torrent = new Torrent(Text.ToString());

        return base.OnEnter(view);
    }

}

public class DownloadButton : Button
{

    private Task _downloadTorrentTask;
    private readonly TorrentFileNameText _torrentFileName;
    private readonly ProgressBar _progressBar;

    public DownloadButton(string name, TorrentFileNameText torrentFileName, ProgressBar progressBar) : base(name)
    {
        _torrentFileName = torrentFileName;
        _progressBar = progressBar;
    }
    public void ButtonPressed()
    {
        _downloadTorrentTask = Task.Run(() => _torrentFileName.Torrent.Download(_progressBar));
    }
}

class MainWindow : Window
{
    public MainWindow(string name) : base(name)
    {
        var menu = new MenuBar(new MenuBarItem[] {
            new MenuBarItem ("_File", new MenuItem [] {
                new MenuItem ("_Quit", "", () => {
                    Application.RequestStop ();
                })
            }),
        });
        var torrentFileLabel = new Label("Torrent File: ")
        {
            X = 2,
            Y = 2
        };

        var torrentFileText = new TorrentFileNameText()
        {
            X = 20,
            Y = Pos.Top(torrentFileLabel),
            Width = 40,
        };

        var progressBarBeginText = new Label("Progress : [")
        {
            X = Pos.Left(torrentFileLabel),
            Y = Pos.Bottom(torrentFileLabel) + 1,
        };
        var torrentDownloadProgress = new ProgressBar()
        {
            X = Pos.Right(progressBarBeginText),
            Y = Pos.Bottom(torrentFileLabel) + 1,
            Width = 60,
            Height = 1
        };

        var progressBarEndText = new Label("]")
        {
            X = Pos.Right(torrentDownloadProgress)-2,
            Y = Pos.Bottom(torrentFileLabel) + 1,
        };

        var downloadButton = new DownloadButton("Download", torrentFileText, torrentDownloadProgress)
        {
            X = Pos.Right(torrentFileText) + 3,
            Y = Pos.Top(torrentFileLabel)

        };

        downloadButton.Clicked += downloadButton.ButtonPressed;

        var downloadStatusBar = new StatusBar();

        Add(menu, torrentFileLabel, torrentFileText, downloadButton, progressBarBeginText, torrentDownloadProgress, progressBarEndText, downloadStatusBar);

    }
}
class Demo
{
    static void Main()
    {

        Application.Init();
        var top = Application.Top;

        var mainApplicationWindow = new MainWindow("BitTorrent Demo Application")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        top.Add(mainApplicationWindow);

        Application.Run();

    }
}
// using System;
// using System.Text;
// using System.Threading;
// using System.IO;
// using NLog;
// using BitTorrentLibrary;

// namespace BitTorrent
// {
//     static class Program
//     {
//         public static void AnnouceResponse(AnnounceResponse response)
//         {
//             BitTorrentLibrary.Log.Logger.Debug("\nAnnouce Response\n-------------");
//             BitTorrentLibrary.Log.Logger.Debug("Status Message: " + response.statusMessage);
//             BitTorrentLibrary.Log.Logger.Debug("Interval: " + response.interval);
//             BitTorrentLibrary.Log.Logger.Debug("Min Interval: " + response.minInterval);
//             BitTorrentLibrary.Log.Logger.Debug("trackerID: " + response.trackerID);
//             BitTorrentLibrary.Log.Logger.Debug("Complete: " + response.complete);
//             BitTorrentLibrary.Log.Logger.Debug("Incomplete: " + response.incomplete);
//             BitTorrentLibrary.Log.Logger.Debug("\nPeers\n------");
//             foreach (var peer in response.peers)
//             {
//                 if (peer._peerID != string.Empty)
//                 {
//                     BitTorrentLibrary.Log.Logger.Debug("Peer ID: " + peer._peerID);
//                 }
//                 BitTorrentLibrary.Log.Logger.Debug("IP: " + peer.ip);
//                 BitTorrentLibrary.Log.Logger.Debug("Port: " + peer.port);
//             }
//         }

//         public static void TorrentHasInfo(MetaInfoFile metaFile)
//         {
//             byte[] infoHash = metaFile.MetaInfoDict["info hash"];

//             StringBuilder hex = new StringBuilder(infoHash.Length);
//             foreach (byte b in infoHash)
//                 hex.AppendFormat("{0:x2}", b);

//             BitTorrentLibrary.Log.Logger.Debug("\nInfo Hash\n-----------\n");
//             BitTorrentLibrary.Log.Logger.Debug(hex);
//         }

//         public static void TorrentTrackers(MetaInfoFile metaFile)
//         {
//             byte[] tracker = metaFile.MetaInfoDict["announce"];

//             BitTorrentLibrary.Log.Logger.Debug("\nTrackers\n--------\n");
//             BitTorrentLibrary.Log.Logger.Debug(Encoding.ASCII.GetString(tracker));

//             if (metaFile.MetaInfoDict.ContainsKey("announce-list"))
//             {
//                 byte[] trackers = metaFile.MetaInfoDict["announce-list"];
//                 BitTorrentLibrary.Log.Logger.Debug(Encoding.ASCII.GetString(trackers));
//             }
//         }

//         public static void Main(string[] args)
//         {

//             try
//             {
//                 for (var test = 0; test < 1; test++)
//                 {

//                     if (File.Exists($"{Directory.GetCurrentDirectory()}/file.txt"))
//                     {
//                         File.Delete($"{Directory.GetCurrentDirectory()}/file.txt");
//                     }

//                     Log.Logger.Info("Loading and parsing metainfo for torrent file ....");
//                     MetaInfoFile torrentFile = new MetaInfoFile("/home/robt/torrent/ipfire.iso.torrent");

//                     torrentFile.Load();
//                     torrentFile.Parse();

//                     Downloader downloader = new Downloader(torrentFile, "/home/robt/utorrent");
//                     Selector selector = new Selector(downloader.Dc);
//                     Assembler assembler = new Assembler(downloader);
//                     Agent agent = new Agent(torrentFile, downloader, assembler);

//                     Tracker tracker = new Tracker(agent, downloader);

//                     tracker.StartAnnouncing();

//                     agent.Start();

//                     agent.Download();

//                     while (true)
//                     {
//                         Thread.Sleep(1000);
//                     }

//                     agent.Close();


//                 }
//             }
//             catch (Error ex)
//             {
//                 BitTorrentLibrary.Log.Logger.Error(ex.Message);
//             }
//             catch (Exception ex)
//             {
//                 BitTorrentLibrary.Log.Logger.Error(ex);
//             }
//         }
//     }
// }
