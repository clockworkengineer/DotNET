//
// Author: Robert Tizzard
//
// Program: Conways Game Of Life.
//
// Description: Console implementation of Conway's game of life a cellular automaton the
// details of which can be  found at https://en.wikipedia.org/wiki/Conway%27s_Game_of_Life.
//
// Copyright 2019.
//

using GameOfLifeLibrary;
using System;
using System.Threading;

namespace ConwaysGameOfLifeConsole
{
    class ConwaysGameOfLife
    {
        /// <summary>
        /// Create life cell grid and follow directed commands.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        public static void Main(string[] args)
        {

            CLifeText cellGrid = new CLifeText(Console.LargestWindowHeight-1, Console.LargestWindowWidth);

            Console.SetCursorPosition(0, Console.WindowHeight-1);
            Console.Write("Commands: 1(Start) 2(Stop) 3(Reset) 4(Quit)");

            RandomizeGrid(cellGrid);

            while (processNextTick(cellGrid))
            {
                cellGrid.nextTick();
                Thread.Sleep(200);
            }

        }

        /// <summary>
        /// Randomizes the game of life cell grid.
        /// </summary>
        /// <param name="cellGrid">Cell grid.</param>
        private static void RandomizeGrid(CLifeText cellGrid) {

            Random randomizer = new Random();

            for (var y = 0; y < cellGrid.CellGridHeight; y++) {
                for (var x = 0; x < cellGrid.CellGridWidth; x++) {
                    cellGrid.setCell(y, x, Convert.ToBoolean((int)randomizer.Next(0, 2)));
                }
            }
        }

        /// <summary>
        /// Check for command key typed and process accordingly.
        /// </summary>
        /// <returns><c>true</c>, command not exit, <c>false</c> otherwise exit.</returns>
        /// <param name="cellGrid">Cell grid.</param>
        private static bool processNextTick(CLifeText cellGrid)
        {
            if (Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).KeyChar)
                {
                    case '1':   // Start automaton
                        cellGrid.start();
                        break;
                    case '2':   // Stop automaton
                        cellGrid.stop();
                        break;
                    case '3':   // Reset automaton
                        cellGrid.stop();
                        RandomizeGrid(cellGrid);
                        cellGrid.Tick = 0;
                        break;
                    case '4':   // Exit and clear
                        Console.Clear();
                        return (false);
                }
            }

            return (true);

        }
    }
}
