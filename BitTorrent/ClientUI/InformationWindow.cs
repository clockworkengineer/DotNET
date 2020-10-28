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
        private readonly Label _infoHashLabel;
        private readonly Label _bytesDownloadedLabel;
        private readonly Label _bytesUploadedLabel;
        private readonly Label _statuslabel;
        private readonly Label _missingPiecesLabel;
        private ListView _peersListView;
        public TextField TrackerText { get; }
        public TextField InfoHashText { get; set; }
        public TextField BytesDownloadedText { get; set; }
        public TextField BytesUploadedText { get; set; }
        public TextField StatusText { get; set; }
        public Window PeersWindow { get; set; }
        public TextField MissingPiecesText { get; set; }

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
                Width = 30,
                Height = Dim.Fill(),
            };
            viewables.Add(PeersWindow);

            _infoHashLabel = new Label("InfoHash:")
            {
                X = Pos.Right(PeersWindow) + 1,
                Y = Pos.Top(PeersWindow)
            };
            viewables.Add(_infoHashLabel);

            InfoHashText = new TextField()
            {
                X = Pos.Right(_infoHashLabel) + 3,
                Y = Pos.Top(_infoHashLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(InfoHashText);

            _bytesDownloadedLabel = new Label("Downloaded:")
            {
                X = Pos.Right(PeersWindow) + 1,
                Y = Pos.Bottom(_infoHashLabel)
            };
            viewables.Add(_bytesDownloadedLabel);

            BytesDownloadedText = new TextField()
            {
                X = Pos.Right(_bytesDownloadedLabel) + 1,
                Y = Pos.Bottom(_infoHashLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(BytesDownloadedText);

            _bytesUploadedLabel = new Label("Uploaded:")
            {
                X = Pos.Left(_infoHashLabel),
                Y = Pos.Bottom(_bytesDownloadedLabel)
            };
            viewables.Add(_bytesUploadedLabel);

            BytesUploadedText = new TextField()
            {
                X = Pos.Right(_bytesDownloadedLabel) + 1,
                Y = Pos.Bottom(_bytesDownloadedLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(BytesUploadedText);

            _missingPiecesLabel = new Label("Missing:")
            {
                X = Pos.Left(_infoHashLabel),
                Y = Pos.Bottom(_bytesUploadedLabel)
            };
            viewables.Add(_missingPiecesLabel);

            MissingPiecesText = new TextField()
            {
                X = Pos.Right(_bytesDownloadedLabel) + 1,
                Y = Pos.Bottom(_bytesUploadedLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(MissingPiecesText);

            _statuslabel = new Label("Status:")
            {
                X = Pos.Left(_infoHashLabel),
                Y = Pos.Bottom(_missingPiecesLabel)
            };
            viewables.Add(_statuslabel);

            StatusText = new TextField()
            {
                X = Pos.Right(_bytesDownloadedLabel) + 1,
                Y = Pos.Bottom(_missingPiecesLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(StatusText);

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
                                  InfoHashText.Text = "";
                                  BytesDownloadedText.Text = "";
                                  BytesUploadedText.Text = "";
                                  MissingPiecesText.Text = "";
                                  StatusText.Text = "Idle";
                                  if (_peersListView != null)
                                  {
                                      PeersWindow.Remove(_peersListView);
                                  }
                              });

        }

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
