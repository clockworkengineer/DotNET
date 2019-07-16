using GameOfLifeLibrary;
using System;
using Gtk;

public partial class MainWindow : Gtk.Window
{
    CLife _cellGrid;

    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();

        int width, height;

        GameOfLifeCellGrid.GdkWindow.GetSize(out width, out height);
        _cellGrid = new CLifeGtk(GameOfLifeCellGrid.GdkWindow, width, height);
        _cellGrid.RandomizeGrid();

        GLib.Timeout.Add(200, new GLib.TimeoutHandler(Update));


    }

    bool Update() {
        
        if (_cellGrid.Running)
        {
            _cellGrid.nextTick();
        }
        else
        {
            _cellGrid.refresh();
        }

        GameOfLifeTickCount.Text = _cellGrid.Tick.ToString();

        return (true);

    }

    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        Application.Quit();
        a.RetVal = true;
    }

    protected void OnStartButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.start();
    }

    protected void OnStopButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();
    }

    protected void OnResetButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();
        _cellGrid.RandomizeGrid();

    }

    protected void OnQuitButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();
        Application.Quit();
    }
}
