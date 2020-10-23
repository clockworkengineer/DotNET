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
        public ProgressBar torrentDownloadProgress;
        public Label progressBarEndText;
        public DownloadButton downloadButton;
        public StatusBar downloadStatusBar;
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

            torrentDownloadProgress = new ProgressBar()
            {
                X = Pos.Right(progressBarBeginText),
                Y = Pos.Bottom(torrentFileLabel) + 1,
                Width = 66,
                Height = 1
            };
            viewables.Add(torrentDownloadProgress);

            progressBarEndText = new Label("]")
            {
                X = Pos.Right(torrentDownloadProgress) - 4,
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

            // var statusItems = new StatusItem [2];

            // statusItems[0] = new StatusItem(Key.ControlD, "Download", ()=> { Console.WriteLine("Download");});
            // statusItems[1] = new StatusItem(Key.ControlI, "Information", ()=> {});

            // downloadStatusBar = new StatusBar(statusItems) 
            // {
            //     X = Pos.Left(this),
            //     Y = Pos.Bottom(progressBarBeginText),
            //     Width = Dim.Fill(),
            //     Height = Dim.Fill()
            // };
            // viewables.Add(downloadStatusBar);

            informationWindow = new InformationWindow("Information")
            {
                X = Pos.Left(this),
                Y = Pos.Bottom(progressBarBeginText),
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            viewables.Add(informationWindow);
            
            foreach (var viewable in viewables)
            {
                Add(viewable);
            }

        }
    }
}
