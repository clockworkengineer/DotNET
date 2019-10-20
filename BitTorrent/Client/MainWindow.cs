using System;
using System.Text;
using Gtk;
using BitTorrent;

public partial class MainWindow : Gtk.Window
{
    private MetaInfoFile _torrentFile;
    private ListStore _filesListViewStore;

    private void AddListViewColumn(string title, int cellNo)
    {
        TreeViewColumn column = new TreeViewColumn();
        column.Title = title;
        CellRendererText cell = new CellRendererText();
        column.PackStart(cell, true);
        filesListView.AppendColumn(column);
        column.AddAttribute(cell, "text", cellNo);
    }

    private void SetText(Entry entry, string field)
    {
        if (_torrentFile.MetaInfoDict.ContainsKey(field))
        {
            entry.IsEditable = false;
            entry.Text = Encoding.ASCII.GetString(_torrentFile.MetaInfoDict[field]);
        }
    }

    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();

        AddListViewColumn("No", 0);
        AddListViewColumn("File Name", 1);
        AddListViewColumn("Length", 2);

        _filesListViewStore = new ListStore(typeof(string), typeof(string), typeof(string));
        filesListView.Model = _filesListViewStore;

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

            _torrentFile = new MetaInfoFile(fileChooser.Filename);

            _torrentFile.load();
            _torrentFile.parse();

            SetText(trackerEntry, "announce");
            SetText(trackersEntry, "announce-list");
            SetText(commentEntry, "comment");
            SetText(createdByEntry, "created by");
            SetText(creationDateEntry, "creation date");
            SetText(pieceLengthEntry, "piece length");

            int fileNo = 0;

            _filesListViewStore.Clear();


            if (!_torrentFile.MetaInfoDict.ContainsKey("0"))
            {
                _filesListViewStore.AppendValues(fileNo.ToString()+1,  
                    Encoding.ASCII.GetString(_torrentFile.MetaInfoDict["name"]), 
                    Encoding.ASCII.GetString(_torrentFile.MetaInfoDict["length"]));
            }
            else
            {
              
                while (_torrentFile.MetaInfoDict.ContainsKey(fileNo.ToString()))
                {
                    string[] rowDetails = Encoding.ASCII.GetString(_torrentFile.MetaInfoDict[fileNo.ToString()]).Split(',');
                    _filesListViewStore.AppendValues((fileNo+1).ToString(), 
                        Encoding.ASCII.GetString(_torrentFile.MetaInfoDict["name"])+rowDetails[0], rowDetails[1]);
                    fileNo++;
                }
            }

        }

    }
}
