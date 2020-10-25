//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: 
//
// Copyright 2020.
//

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
            new StatusItem(Key.ControlD, "~^D~ Download", () => {}),
            new StatusItem(Key.ControlS, "~^S~ shutdown", () => { mainApplicationWindow.TorrentFileText.Torrent.DownloadAgent.Close();}),
            new StatusItem(Key.ControlQ, "~^Q~ Quit", () => {  top.Running = false;  })
            });

            top.Add(mainApplicationWindow, downloadStatusBar);

            Application.Run();

        }
    }
}
