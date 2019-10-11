using System;
using Gtk;
using BitTorrent;

public partial class MainWindow : Gtk.Window
{
    MetaInfoFile torrentFile;

    private void SetText(Entry entry, string field)
    {
        if (torrentFile.MetaInfoDict.ContainsKey(field))
        {
            entry.Text = torrentFile.MetaInfoDict[field];
        }
    }

    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();
    }

    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        Application.Quit();
        a.RetVal = true;
    }

    protected void OnFilechooserwidget1FileActivated(object sender, EventArgs e)
    {
        FileChooser fileChooser = (Gtk.FileChooser)sender;
     

        if (fileChooser.Filename.LastIndexOf(".torrent", System.StringComparison.Ordinal) + 8 == fileChooser.Filename.Length)
        {
            Console.WriteLine(fileChooser.Filename);

            torrentFile = new MetaInfoFile(fileChooser.Filename);

            torrentFile.load();
            torrentFile.parse();

            SetText(trackerEntry, "announce");
            SetText(trackersEntry, "announce-list");
            SetText(commentEntry, "comment");
            SetText(createdByEntry, "created by");
            SetText(creationDateEntry, "creation date");
            SetText(pieceLengthEntry, "piece length");

        }

    }
}
