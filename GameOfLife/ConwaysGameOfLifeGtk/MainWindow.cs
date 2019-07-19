//
// Author: Robert Tizzard
// 
// Class: MainWindow
//
// Description: Main window for C# Conway's game of life a cellular automaton.
// 
// Copyright 2019.
//

using GameOfLifeLibrary;
using System;
using Gtk;

public partial class MainWindow : Gtk.Window
{
    private  const int kDefaultScaleFactor = 4; // Default cell grid pixel size
    private  const int kUpdateTimer = 200;      // Default cell grid window update timer

    private CLifeGtk _cellGrid;                 // Cell grid automaton

    /// <summary>
    /// Enable/disable a UI button.
    /// </summary>
    /// <param name="button">Button.</param>
    /// <param name="enabled">If set to <c>true</c> enabled.</param>
    private void ButtonEnable(Button button, bool enabled) {
        if (enabled) {
            button.State = StateType.Normal;
            button.Sensitive = true;
        } else {
            button.State = StateType.Insensitive;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:MainWindow"/> class.
    /// </summary>
    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        // Gtk # house keeping

        Build();

        // Get width and height of cell grid draw area

        int width, height;
        GameOfLifeCellGrid.GdkWindow.GetSize(out width, out height);

        // Create cell grid automaton for draw area (scaling down for pixel size

        _cellGrid = new CLifeGtk(GameOfLifeCellGrid.GdkWindow, width / kDefaultScaleFactor, height / kDefaultScaleFactor);
        _cellGrid.ScaleFactor = kDefaultScaleFactor;

        // Create random cell pattern

        _cellGrid.RandomizeGrid();

        // Start cell grid updater

        GLib.Timeout.Add(kUpdateTimer, new GLib.TimeoutHandler(Update));

        // Set initial button states

        ButtonEnable(StopButton, false);

    }

    /// <summary>
    /// Update display context from cell grid.
    /// </summary>
    /// <returns>The update.</returns>
    bool Update() {

        // If running then perform next automaton cycle otherwise just display
        // current cell grid contents

        if (_cellGrid.Running)
        {
            _cellGrid.nextTick();
        }
        else
        {
            _cellGrid.refresh();
        }

        // Display current tick count

        GameOfLifeTickCount.Text = _cellGrid.Tick.ToString();

        return (true);

    }

    /// <summary>
    /// On the delete event quit application.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="a">The alpha component.</param>
    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        Application.Quit();
        a.RetVal = true;
    }

    /// <summary>
    /// On the start button clicked flag automaton as running.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnStartButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.start();

        ButtonEnable(StartButton, false);
        ButtonEnable(ResetButton, false);
        ButtonEnable(QuitButton, false);
        ButtonEnable(StopButton, true);

    }

    /// <summary>
    /// On the stop button clicked stop automaton.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnStopButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();

        ButtonEnable(StartButton, true);
        ButtonEnable(ResetButton, true);
        ButtonEnable(QuitButton, true);
        ButtonEnable(StopButton, false);
 
    }

    /// <summary>
    /// On the reset button clicked stop automaton, randomize grid and display.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnResetButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();
        _cellGrid.drawInActiveCells();
        _cellGrid.RandomizeGrid();

        ButtonEnable(StartButton, true);
        ButtonEnable(ResetButton, true);
        ButtonEnable(QuitButton, true);
        ButtonEnable(StopButton, false);

    }

    /// <summary>
    /// On the quit button clicked stop automaton and quit application.
    /// </summary>
    /// <param name="sender">Sender.</param>
    /// <param name="e">E.</param>
    protected void OnQuitButtonClicked(object sender, EventArgs e)
    {
        _cellGrid.stop();
        Application.Quit();
    }
}
