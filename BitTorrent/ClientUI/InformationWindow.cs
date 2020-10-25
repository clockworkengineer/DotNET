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
        public TextField _infoHashText;
        public TextField _bytesDownloadedText;
        public TextField _bytesUploadedText;
        public Window peersWindow;
        public ListView peersListView;
        public TextField TrackerText { get; }
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

            peersWindow = new Window("Peers")
            {
                X = Pos.Left(_trackerLabel),
                Y = Pos.Bottom(_trackerLabel) + 1,
                Width = 20,
                Height = Dim.Fill(),
            };
            viewables.Add(peersWindow);

            _infoHashLabel = new Label("InfoHash:")
            {
                X = Pos.Right(peersWindow) + 1,
                Y = Pos.Top(peersWindow)
            };
            viewables.Add(_infoHashLabel);

            _infoHashText = new TextField()
            {
                X = Pos.Right(_infoHashLabel) + 3,
                Y = Pos.Top(_infoHashLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(_infoHashText);

            _bytesDownloadedLabel = new Label("Downloaded:")
            {
                X = Pos.Right(peersWindow) + 1,
                Y = Pos.Bottom(_infoHashLabel)
            };
            viewables.Add(_bytesDownloadedLabel);

            _bytesDownloadedText = new TextField()
            {
                X = Pos.Right(_bytesDownloadedLabel) + 1,
                Y = Pos.Bottom(_infoHashLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(_bytesDownloadedText);

            _bytesUploadedLabel = new Label("Uploaded:")
            {
                X = Pos.Left(_infoHashLabel),
                Y = Pos.Bottom(_bytesDownloadedLabel)
            };
            viewables.Add(_bytesUploadedLabel);

            _bytesUploadedText = new TextField()
            {
                X = Pos.Right(_bytesDownloadedLabel) + 1,
                Y = Pos.Bottom(_bytesDownloadedLabel),
                Width = 20,
                CanFocus = false
            };
            viewables.Add(_bytesUploadedText);

            foreach (var viewable in viewables)
            {
                Add(viewable);
            }
        }


    }
}
