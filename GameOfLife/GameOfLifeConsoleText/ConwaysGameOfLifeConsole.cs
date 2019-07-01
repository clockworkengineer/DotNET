using GameOfLifeLibrary;
using System;
using System.Threading;

namespace ConwaysGameOfLifeConsole
{
    class MainClass
    {
        public static void Main(string[] args)
        {

            CLifeText life = new CLifeText(Console.WindowHeight, Console.WindowWidth);

            RandomizeGrid(life);

            life.start();
            while (!Console.KeyAvailable)
            {
                Thread.Sleep(200);
                life.nextTick();
            }
            life.stop();

            Console.Read();

        }

        private static void RandomizeGrid(CLifeText life) {

            Random randomizer = new Random();

            for (var y = 0; y < life.CellGridHeight; y++) {
                for (var x = 0; x < life.CellGridWidth; x++) {
                    life.setCell(y, x, Convert.ToBoolean((int)randomizer.Next(0, 2)));
                }
            }
        }
    }
}
