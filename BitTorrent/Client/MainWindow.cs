using System;
using System.Text;
using System.Threading.Tasks;

using Gtk;
using BitTorrent;

public partial class MainWindow : Gtk.Window
{
    private MetaInfoFile _torrentFile;
    private FileAgent _torrentFileAgent;
    private ListStore _filesListViewStore;
    private ListStore _peersListViewStore;

    private void AddListViewColumn(TreeView listView, string title, int cellNo)
    {
        TreeViewColumn column = new TreeViewColumn();
        column.Title = title;
        CellRendererText cell = new CellRendererText();
        column.PackStart(cell, true);
        listView.AppendColumn(column);
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
    
    static private void updateDownloadDetails(MainWindow window)
    {
        double percent = 0;

        if (window._torrentFileAgent != null)
        {
            percent = window._torrentFileAgent.FileToDownloader.Dc.totalBytesDownloaded / (double)window._torrentFileAgent.FileToDownloader.Dc.totalLength;
        }
        window.progressbarDownload.Fraction = percent;
        window.progressbarDownload.Text = (percent).ToString("0.00%");

        window._peersListViewStore.Clear();

        int peerNo = 0;
        foreach (var peer in window._torrentFileAgent.CurrentAnnouneResponse.peers)
        {
            window._peersListViewStore.AppendValues((peerNo + 1).ToString(), peer.ip, peer.port.ToString());
            peerNo++;
        }

        window.seedPeersEntry.Text = window._torrentFileAgent.CurrentAnnouneResponse.complete.ToString();
        window.downloadingPeersEntry.Text = window._torrentFileAgent.CurrentAnnouneResponse.incomplete.ToString();
        window.downloadedEntry.Text = window._torrentFileAgent.FileToDownloader.Dc.totalBytesDownloaded.ToString();
        window.uploadedEntry.Text = "0";

    }

    static void updateDownloadStatus(System.Object source)
    {
        MainWindow window = (MainWindow)source;
        Gtk.Application.Invoke(delegate { updateDownloadDetails(window); });
    }

    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();

        AddListViewColumn(filesListView, "No", 0);
        AddListViewColumn(filesListView, "File Name", 1);
        AddListViewColumn(filesListView, "Length", 2);

        _filesListViewStore = new ListStore(typeof(string), typeof(string), typeof(string));
        filesListView.Model = _filesListViewStore;

        _peersListViewStore = new ListStore(typeof(string), typeof(string), typeof(string));
        peersListView.Model = _peersListViewStore;

        AddListViewColumn(peersListView, "No", 0);
        AddListViewColumn(peersListView, "Peer", 1);
        AddListViewColumn(peersListView, "Port", 2);

        downloadButton.Sensitive = false;
        pauseContinueButton.Sensitive = false;

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

            _torrentFile.Load();
            _torrentFile.Parse();

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

            byte[] infoHash = _torrentFile.MetaInfoDict["info hash"];

            StringBuilder hex = new StringBuilder(infoHash.Length);
            foreach (byte b in infoHash)
            {
                hex.AppendFormat("{0:x2}", b);
            }

            infoHashEntry.Text = hex.ToString();

            _torrentFileAgent = new FileAgent(fileChooser.Filename, "/home/robt/utorrent/");
            downloadButton.Sensitive = true;
       

        }

    }

    static void updateProgressBarCallBack(System.Object arg)
    {
        MainWindow window = (MainWindow) arg;

        updateDownloadDetails(window);

    }
 
    async protected void OnDownloadButtonClicked(object sender, EventArgs e)
    {

        downloadButton.Sensitive = false;
        pauseContinueButton.Sensitive = true;

        await _torrentFileAgent.LoadAsync();

        await _torrentFileAgent.DownloadAsync(updateDownloadStatus, this);

        downloadButton.Sensitive = true;
        pauseContinueButton.Sensitive = false;

    }

    protected void OnPauseContinueButtonClicked(object sender, EventArgs e)
    {
        if (_torrentFileAgent.Downloading.WaitOne(0))
        {
            pauseContinueButton.Label = "Continue";
            _torrentFileAgent.Pause();
        }
        else
        {
            pauseContinueButton.Label = "Pause";
            _torrentFileAgent.Start();
        }
    }
}
