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
        private readonly Label _trackerLabel;
        private readonly TextField _trackerText;
        public Window peersWindow;
        public ListView peersListView;

        public InformationWindow(string name) : base(name)
        {
            List<View> viewables = new List<View>();

             _trackerLabel = new Label("Tracker : ")
            {
                X = 1,
                Y = 1
            };
            viewables.Add(_trackerLabel);

             _trackerText = new TextField()
            {
                X = Pos.Right(_trackerLabel),
                Y = Pos.Top(_trackerLabel),
                Width = 70,
                CanFocus = false
            };
            viewables.Add(_trackerText);

             peersWindow = new Window("Peers")
            {
                X = Pos.Left(_trackerLabel),
                Y = Pos.Bottom(_trackerLabel)+1,
                Width = 20,
                Height = Dim.Fill(),
            };
            viewables.Add(peersWindow);

            foreach (var viewable in viewables)
            {
                Add(viewable);
            }
        }

    }
}
