//
// Author: Robert Tizzard
// 
// Class: CLife
//
// Description: Parent class implementation of CLife that does cell grid output using the Cairo 2D vector library.
// 
// Copyright 2019.
//

using System;
using Gdk;
using Cairo;

namespace GameOfLifeLibrary
{
    public class CLifeGtk : CLife
    {

        private Context _cellGridContext;       // Cell grid Cairo drawing context
        private int _scaleFactor;               // Cell grid scale factor for display grid
        private Cairo.Color _inactiveCellColor; // Cell inactive color
        private Cairo.Color _activeCellColor;   // Cell active color

        public int ScaleFactor { get => _scaleFactor; set => _scaleFactor = value; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:GameOfLifeLibrary.CLifeGtk"/> class.
        /// </summary>
        /// <param name="cellGridWindow">Cell grid draw window.</param>
        /// <param name="height">Height.</param>
        /// <param name="width">Width.</param>
        public CLifeGtk(Gdk.Window cellGridWindow, int height, int width) : base(height,width)
        {
            // Create cell grid display context

            _cellGridContext = Gdk.CairoHelper.Create(cellGridWindow);
            if (_cellGridContext==null)
            {
                throw new CLifeException("Could not create Cairo context for cell grid.");
            }

            // Set default context values

            _cellGridContext.Antialias = Cairo.Antialias.None;
            _cellGridContext.Rotate(0);
            _cellGridContext.Translate(0, 0);
            _cellGridContext.LineWidth = 1;

            // Set other defaults

            _scaleFactor = 1;
            _inactiveCellColor = new Cairo.Color(0.95, 0.95, 0.95); // White
            _activeCellColor = new Cairo.Color(0, 0, 0);            // Black


    }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="T:GameOfLifeLibrary.CLifeGtk"/> is reclaimed by garbage collection.
        /// </summary>
        ~CLifeGtk()
        {
            // Dispose of cell grid context

            _cellGridContext.GetTarget().Dispose();
            _cellGridContext.Dispose();

        }

        /// <summary>
        /// Refresh cell grid display context from current cell automaton.
        /// </summary>
        public override void refresh()
        {
            // If automaton running fill in draw area with cell inactive color

            if (Running) {
                clearDrawArea();
            }

            // Fill in rest of drawing area with active cells

            _cellGridContext.SetSourceColor(_activeCellColor);

            for (int y = 0, scaleY = 0; y < CellGridHeight; y++, scaleY += ScaleFactor)
            {
                for (int  x = 0, scaleX = 0; x < CellGridWidth; x++, scaleX += ScaleFactor)
                {
                    if (getCell(y, x))
                    {
                        _cellGridContext.Rectangle(new Cairo.Rectangle(scaleX, scaleY, ScaleFactor, ScaleFactor));
                        _cellGridContext.Fill();
                    }
                }
            }
        }

        /// <summary>
        /// Clears the cell grid context area (fill with inactive color).
        /// </summary>
        public void clearDrawArea() {
            _cellGridContext.SetSourceColor(_inactiveCellColor);
            _cellGridContext.Rectangle(new Cairo.Rectangle(0, 0, CellGridWidth * ScaleFactor, CellGridHeight * ScaleFactor));
            _cellGridContext.Fill();
        }

        /// <summary>
        /// Sets the color of an active grid cell.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public void setActiveColor(double r, double g, double b)
        {
            _activeCellColor = new Cairo.Color(r, g, b);
        }

    }
}