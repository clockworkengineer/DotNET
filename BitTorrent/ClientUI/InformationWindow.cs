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
    /// <summary>
    /// 
    /// </summary>
    public class InformationWindow : Window
    {
        private readonly Label _trackerLabel;
        private ListView _peersListView;
        public TextField TrackerText { get; }
        public TextField[] InfoTextFields;
        public Window PeersWindow { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public InformationWindow(string name) : base(name)
        {
            List<View> viewables = new List<View>();

            _trackerLabel = new Label("Tracker : ")
            {
                X = 1,
                Y = 1
            };
            viewables.Add(_trackerLabel);
            TrackerText = new TextField()
            {
                X = Pos.Right(_trackerLabel),
                Y = Pos.Top(_trackerLabel),
                Width = 70,
                CanFocus = false
            };
            viewables.Add(TrackerText);
            PeersWindow = new Window("Peers")
            {
                X = Pos.Left(_trackerLabel),
                Y = Pos.Bottom(_trackerLabel) + 1,
                Width = 23,
                Height = Dim.Fill(),
            };
            viewables.Add(PeersWindow);

            string[] labels = { "InfoHash:", "Downloaded:", "Uploaded:", "Missing:", "Status:","Swarm:", "Dead:" };

            InfoTextFields = new TextField[labels.Length];

            int pos = 0;
            foreach (var label in labels)
            {
                Label text = new Label(label)
                {
                    X = Pos.Right(PeersWindow) + 1,
                    Y = Pos.Top(PeersWindow) + pos
                };
                viewables.Add(text);
                InfoTextFields[pos] = new TextField()
                {
                    X = Pos.Right(PeersWindow) + 12,
                    Y = Pos.Bottom(_trackerLabel) + pos + 1,
                    Width = 40,
                    CanFocus = false
                };
                viewables.Add(InfoTextFields[pos]);
                pos++;
            }

            foreach (var viewable in viewables)
            {
                Add(viewable);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void ClearData()
        {
            Application.MainLoop.Invoke(() =>
                              {
                                  InfoTextFields[0].Text = "";
                                  InfoTextFields[1].Text = "";
                                  InfoTextFields[2].Text = "";
                                  InfoTextFields[3].Text = "";
                                  InfoTextFields[4].Text = "Idle";
                                  InfoTextFields[5].Text = "";
                                  InfoTextFields[6].Text = "";
                                  if (_peersListView != null)
                                  {
                                      PeersWindow.Remove(_peersListView);
                                  }
                              });

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="peersList"></param>
        public void UpdatePeers(ListView peersList)
        {
            if (_peersListView != null)
            {
                PeersWindow.Remove(_peersListView);
            }
            _peersListView = peersList;
            PeersWindow.Add(_peersListView);
        }

    }
}
