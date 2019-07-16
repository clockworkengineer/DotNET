using GameOfLifeLibrary;
using System;
using Gtk;

public partial class MainWindow : Gtk.Window
{
    private CLife _cellGrid;

    private void ButtonEnable(Button button, bool enabled) {
        if (enabled) {
            button.State = StateType.Normal;
            button.Sensitive = true;
        } else {
            button.State = StateType.Insensitive;
        }
    }

    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();

        int width, height;

        GameOfLifeCellGrid.GdkWindow.GetSize(out width, out height);
        _cellGrid = new CLifeGtk(GameOfLifeCellGrid.GdkWindow, width, height);
        _cellGrid.RandomizeGrid();

        GLib.Timeout.Add(200, new GLib.TimeoutHandler(Update));

        ButtonEnable(StopButton, false);

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

        ButtonEnable(StartButton, false);
        ButtonEnable(ResetButton, false);
        ButtonEnable(QuitButton, false);
        ButtonEnable(StopButton, true);

    }

    protected void OnStopButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();

        ButtonEnable(StartButton, true);
        ButtonEnable(ResetButton, true);
        ButtonEnable(QuitButton, true);
        ButtonEnable(StopButton, false);
 
    }

    protected void OnResetButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();
        _cellGrid.RandomizeGrid();

        ButtonEnable(StartButton, true);
        ButtonEnable(ResetButton, true);
        ButtonEnable(QuitButton, true);
        ButtonEnable(StopButton, false);

    }

    protected void OnQuitButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();
        Application.Quit();
    }
}
