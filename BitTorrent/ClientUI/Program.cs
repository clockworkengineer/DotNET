//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: 
//
// Copyright 2020.
//

using System.Threading.Tasks;
using Terminal.Gui;

namespace ClientUI
{
    /// <summary>
    /// 
    /// </summary>
    class DemoTorrentApplication
    {
        /// <summary>
        /// 
        /// </summary>
        static void Main()
        {

            Application.Init();
            var top = Application.Top;

            var mainWindow = new MainWindow("BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var downloadStatusBar = new StatusBar(new StatusItem[] {
            new StatusItem(Key.ControlD, "~^D~ Download", () => {
                if (!mainWindow.DownloadingTorrent)
                {
                    mainWindow.Torrent = new Torrent(mainWindow.TorrentFileText.Text.ToString());
                    mainWindow.DownloadTorrentTask = Task.Run(() => mainWindow.Torrent.Download(mainWindow));
                    mainWindow.DownloadingTorrent = true;
                } else {
                    MessageBox.Query("Information", "Already downloading torrent. You need to shut it down.", "Ok");
                }
            }),
            new StatusItem(Key.ControlS, "~^S~ shutdown", () =>
            {
                if(mainWindow.DownloadingTorrent) {
                    lock (mainWindow.StartupLock) {
                        mainWindow.Torrent.DownloadAgent.Close(mainWindow.Torrent.Tc);
                        mainWindow.DownloadingTorrent = false;
                        mainWindow.InformationWindow.ClearData();
                    }
                }
            }),
            new StatusItem(Key.ControlQ, "~^Q~ Quit", () => {  top.Running = false;  })
            });

            top.Add(mainWindow, downloadStatusBar);

            Application.Run();

        }
    }
}
