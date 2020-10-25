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

            var mainApplicationWindow = new MainWindow("BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var downloadStatusBar = new StatusBar(new StatusItem[] {
            new StatusItem(Key.ControlD, "~^D~ Download", () => {  if (!mainApplicationWindow.DownloadingTorrent)
            {
                mainApplicationWindow.Torrent = new Torrent(mainApplicationWindow.TorrentFileText.Text.ToString());
                mainApplicationWindow.DownloadTorrentTask = Task.Run(() => mainApplicationWindow.Torrent.Download(mainApplicationWindow));
                mainApplicationWindow.DownloadingTorrent = true;
            }}),
            new StatusItem(Key.ControlS, "~^S~ shutdown", () =>
            {
                mainApplicationWindow.Torrent.DownloadAgent.Close();

            }),
            new StatusItem(Key.ControlQ, "~^Q~ Quit", () => {  top.Running = false;  })
            });

            top.Add(mainApplicationWindow, downloadStatusBar);

            Application.Run();

        }
    }
}
