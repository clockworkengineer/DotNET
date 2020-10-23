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

    public class MainWindow : Window
    {

        public MenuBar mainMenuBar;
        public Label torrentFileLabel;
        public TorrentFileNameText torrentFileText;
        public Label progressBarBeginText;
        public ProgressBar downloadProgress;
        public Label progressBarEndText;
        public DownloadButton downloadButton;
        public InformationWindow informationWindow;


        public MainWindow(string name) : base(name)
        {
            List<View> viewables = new List<View>();

            torrentFileLabel = new Label("Torrent File: ")
            {
                X = 1,
                Y = 1
            };
            viewables.Add(torrentFileLabel);

            torrentFileText = new TorrentFileNameText()
            {
                X = Pos.Right(torrentFileLabel),
                Y = Pos.Top(torrentFileLabel),
                Width = 50,
            };
            viewables.Add(torrentFileText);

            progressBarBeginText = new Label("Progress : [")
            {
                X = Pos.Left(torrentFileLabel),
                Y = Pos.Bottom(torrentFileLabel) + 1,
            };
            viewables.Add(progressBarBeginText);

            downloadProgress = new ProgressBar()
            {
                X = Pos.Right(progressBarBeginText),
                Y = Pos.Bottom(torrentFileLabel) + 1,
                Width = 60,
                Height = 1
            };
            viewables.Add(downloadProgress);

            progressBarEndText = new Label("]")
            {
                X = Pos.Right(downloadProgress) - 1,
                Y = Pos.Bottom(torrentFileLabel) + 1,
            };
            viewables.Add(progressBarEndText);

            downloadButton = new DownloadButton("Download", this)
            {
                X = Pos.Right(torrentFileText),
                Y = Pos.Top(torrentFileLabel)

            };
            viewables.Add(downloadButton);

            downloadButton.Clicked += downloadButton.ButtonPressed;

            informationWindow = new InformationWindow("Information")
            {
                X = Pos.Left(this),
                Y = Pos.Bottom(progressBarBeginText)+1,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus=false
            };
            viewables.Add(informationWindow);
            
            foreach (var viewable in viewables)
            {
                Add(viewable);
            }

        }
    }
}
