//
// Author: Robert Tizzard
//
// Program: Conways Game Of Life.
//
// Description: C# GTK#/Cairo implementation of Conway's game of life a cellular automaton the
// details of which can be  found at https://en.wikipedia.org/wiki/Conway%27s_Game_of_Life.
//
// Copyright 2019.
//

using GameOfLifeLibrary;
using System;
using Gtk;

namespace ConwaysGameOfLifeGtk
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            try
            {
                Application.Init();
                MainWindow win = new MainWindow();
                win.Show();
                Application.Run();
            }
            catch (CLifeException e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
