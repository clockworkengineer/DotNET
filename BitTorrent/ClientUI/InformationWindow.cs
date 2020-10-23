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
    public class InformationWindow : Window
    {
        public Label trackerLabel;
        public TextField trackerText;
        public Window peersWindow;
        public ListView peersListView;

        public InformationWindow(string name) : base(name)
        {
            List<View> viewables = new List<View>();

             trackerLabel = new Label("Tracker : ")
            {
                X = 1,
                Y = 1
            };
            viewables.Add(trackerLabel);

             trackerText = new TextField()
            {
                X = Pos.Right(trackerLabel),
                Y = Pos.Top(trackerLabel),
                Width = 70,
            };
            viewables.Add(trackerText);

             peersWindow = new Window("Peers")
            {
                X = Pos.Left(trackerLabel),
                Y = Pos.Bottom(trackerLabel),
                Width = 20,
                Height = Dim.Fill(),
            };
            viewables.Add(peersWindow);

            //  peersListView = new ListView()
            // {
            //     X = 0,
            //     Y = 0,
            //     Width = Dim.Fill(),
            //     Height = Dim.Fill()
            // };
            // peersWindow.Add(peersListView);

            foreach (var viewable in viewables)
            {
                Add(viewable);
            }
        }

    }
}
