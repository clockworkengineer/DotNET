using GameOfLifeLibrary;
using System;
using Gtk;

namespace ConwaysGameOfLifeGtk
{
    public partial class GameOfLifeMainWindow : Gtk.Window
    {
        public GameOfLifeMainWindow() :
                base(Gtk.WindowType.Toplevel)
        {
            this.Build();
        }
    }
}
