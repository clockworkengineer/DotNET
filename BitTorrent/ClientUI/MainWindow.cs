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
using BitTorrentLibrary;
using Terminal.Gui;

namespace ClientUI
{
    class MainWindow : Window
    {
        public MainWindow(string name) : base(name)
        {
            List<View> viewables = new List<View>();

            var mainMenuBar = new MenuBar(new MenuBarItem[] {
            new MenuBarItem ("_File", new MenuItem [] {
                new MenuItem ("_Quit", "", () => {
                    Application.RequestStop ();
                })
            }),
        });
            viewables.Add(mainMenuBar);

            var torrentFileLabel = new Label("Torrent File: ")
            {
                X = 2,
                Y = 2
            };
            viewables.Add(torrentFileLabel);

            var torrentFileText = new TorrentFileNameText()
            {
                X = 20,
                Y = Pos.Top(torrentFileLabel),
                Width = 40,
            };
            viewables.Add(torrentFileText);

            var progressBarBeginText = new Label("Progress : [")
            {
                X = Pos.Left(torrentFileLabel),
                Y = Pos.Bottom(torrentFileLabel) + 1,
            };
            viewables.Add(progressBarBeginText);

            var torrentDownloadProgress = new ProgressBar()
            {
                X = Pos.Right(progressBarBeginText),
                Y = Pos.Bottom(torrentFileLabel) + 1,
                Width = 66,
                Height = 1
            };
            viewables.Add(torrentDownloadProgress);

            var progressBarEndText = new Label("]")
            {
                X = Pos.Right(torrentDownloadProgress) - 4,
                Y = Pos.Bottom(torrentFileLabel) + 1,
            };
            viewables.Add(progressBarEndText);

            var downloadButton = new DownloadButton("Download", torrentFileText, torrentDownloadProgress)
            {
                X = Pos.Right(torrentFileText) + 3,
                Y = Pos.Top(torrentFileLabel)

            };
            viewables.Add(downloadButton);

            downloadButton.Clicked += downloadButton.ButtonPressed;

            var downloadStatusBar = new StatusBar();
            viewables.Add(downloadStatusBar);

            foreach (var viewable in viewables)
            {
                Add(viewable);
            }

        }
    }
}
