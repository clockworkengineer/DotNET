using System;

namespace GameOfLifeLibrary
{
    public class CLifeText : CLife
    {
        public CLifeText(int height, int width) : base(height, width)
        {
        }

        public override void updateCell(int y, int x, bool active)
        {
            Console.SetCursorPosition(x, y);
            Console.Write(active ? '*' : ' ');
        }
    }
}
