using GameOfLifeLibrary;
using System;
using System.Threading;

namespace ConwaysGameOfLifeConsole
{
    class MainClass
    {
        public static void Main(string[] args)
        {

            CLifeText cellGrid = new CLifeText(Console.WindowHeight-1, Console.WindowWidth);

            Console.SetCursorPosition(0, Console.WindowHeight-1);
            Console.Write("Commands: 1(Start) 2(Stop) 3(Reset) 4(Quit)");

            RandomizeGrid(cellGrid);

            while (processNextTick(cellGrid))
            {
                cellGrid.nextTick();
                Thread.Sleep(200);
            }

        }

        private static void RandomizeGrid(CLifeText cellGrid) {

            Random randomizer = new Random();

            for (var y = 0; y < cellGrid.CellGridHeight; y++) {
                for (var x = 0; x < cellGrid.CellGridWidth; x++) {
                    cellGrid.setCell(y, x, Convert.ToBoolean((int)randomizer.Next(0, 2)));
                }
            }
        }

        private static bool processNextTick(CLifeText cellGrid)
        {
            if (Console.KeyAvailable)
            {

                switch (Console.ReadKey(true).KeyChar)
                {
                    case '1':
                        cellGrid.start();
                        break;
                    case '2':
                        cellGrid.stop();
                        break;
                    case '3':
                        cellGrid.stop();
                        RandomizeGrid(cellGrid);
                        cellGrid.Tick = 0;

                        break;
                    case '4':
                        Console.Clear();
                        return (false);
                }
            }

            return (true);

        }
    }
}
