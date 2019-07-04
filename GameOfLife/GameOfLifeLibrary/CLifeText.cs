//
// Author: Robert Tizzard
// 
// Class: CLife
//
// Description: Parent class implementation of CLife that does text output a C# console.
// 
// Copyright 2019.
//
using System;

namespace GameOfLifeLibrary
{
    public class CLifeText : CLife
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:GameOfLifeLibrary.CLifeText"/> class.
        /// </summary>
        /// <param name="height">Height.</param>
        /// <param name="width">Width.</param>
        public CLifeText(int height, int width) : base(height, width)
        {
        }

        /// <summary>
        /// Updates a cell at x,y on the console. ' ' inactive cells and '*' being active cells.
        /// </summary>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        /// <param name="active">If set to <c>true</c> active.</param>
        public override void updateCell(int y, int x, bool active)
        {
            Console.SetCursorPosition(x, y);
            Console.Write(active ? '*' : ' ');
        }
    }
}
