//
// Author: Robert Tizzard
// 
// Class: CLife
//
// Description: Base class implementation of Conway's game of life a cellular automaton.
// 
// Copyright 2019.
//

using System;

namespace GameOfLifeLibrary
{
    public class CLife
    {
        private bool _running=false;        // true then running automaton
        private int _tick = 0;              // Current tick
        private int _cellGridWidth = 0;     // Cell grid width
        private int _cellGridHeight = 0;    // Cell grid height
        private bool[] _cellMasterGrid;     // Master cell grid
        private bool[] _cellGridReadOnly;   // Readonly copy of master cell grid

        public bool Running { get => _running; set => _running = value; }
        public int Tick { get => _tick; set => _tick = value; }
        public int CellGridWidth { get => _cellGridWidth; protected set => _cellGridWidth = value; }
        public int CellGridHeight { get => _cellGridHeight; protected set => _cellGridHeight = value; }

        /// <summary>
        /// Returns index of cell at y, x.
        /// </summary>
        /// <returns>The index.</returns>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        private int cellIndex(int y, int x)
        {
            return ((y * CellGridWidth) + x);
        }

        /// <summary>
        /// Perform bounds checking on coordinates and wrap them if necessary.
        /// </summary>
        /// <returns>The y, x coordinates. Wrapping them around if the go out of bounds.</returns>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        private Tuple<int, int> gridBounds(int y, int x)
        {

            if (y < 0)
            {
                y = CellGridHeight - 1;
            }
            else if (y == CellGridHeight)
            {
                y = 0;
            }

            if (x < 0)
            {
                x = CellGridWidth - 1;
            }
            else if (x == CellGridWidth)
            {
                x = 0;
            }

            return (new Tuple<int, int>(y, x));

        }

        /// <summary>
        /// Determine whether a grid cell is active.
        /// </summary>
        /// <returns><c>true</c>, if cell active then true, <c>false</c> otherwise.</returns>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        private bool isCellActive(int y, int x)
        {

            var coords = gridBounds(y, x);

            return (_cellGridReadOnly[cellIndex(coords.Item1, coords.Item2)]);

        }

        /// <summary>
        /// Determine number of active neighbours of a given grid cell.
        /// </summary>
        /// <returns>The number of active cell neighbours.</returns>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        private int activeCellNeighbours(int y, int x)
        {

            int activeNeighbours = 0;

            if (isCellActive(y - 1, x - 1))
            {
                activeNeighbours++;
            }
            if (isCellActive(y - 1, x))
            {
                activeNeighbours++;
            }
            if (isCellActive(y - 1, x + 1))
            {
                activeNeighbours++;
            }
            if (isCellActive(y, x - 1))
            {
                activeNeighbours++;
            }
            if (isCellActive(y, x + 1))
            {
                activeNeighbours++;
            }
            if (isCellActive(y + 1, x - 1))
            {
                activeNeighbours++;
            }
            if (isCellActive(y + 1, x))
            {
                activeNeighbours++;
            }
            if (isCellActive(y + 1, x + 1))
            {
                activeNeighbours++;
            }

            return (activeNeighbours);

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:GameOfLifeLibrary.CLife"/> class.
        /// </summary>
        /// <param name="height">Height.</param>
        /// <param name="width">Width.</param>
        public CLife(int height, int width)
        {
            CellGridWidth = width;
            CellGridHeight = height;
            _cellMasterGrid = new bool[CellGridWidth * CellGridHeight];
            _cellGridReadOnly = new bool[CellGridWidth * CellGridHeight];
        }

        /// <summary>
        /// Actives/deativates a cell at coordinates x,y.
        /// </summary>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        /// <param name="active">If set to <c>true</c> active.</param>
        public void setCell(int y, int x, bool active)
        {
            var coords = gridBounds(y, x);

            _cellMasterGrid[cellIndex(coords.Item1, coords.Item2)] = active;

            updateCell(y, x, active);
        }

        /// <summary>
        /// Gets the cell and cordinate x,y active state.
        /// </summary>
        /// <returns><c>true</c>, if cell was active, <c>false</c> otherwise.</returns>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        public bool getCell(int y, int x) 
        {
            var coords = gridBounds(y, x);

            return (_cellMasterGrid[cellIndex(coords.Item1, coords.Item2)]);
        }

        /// <summary>
        /// Randomizes cell grid.
        /// </summary>
        /// <param name="cellGrid">Cell grid.</param>
        public void RandomizeGrid()
        {

            Random randomizer = new Random();

            for (var y = 0; y < CellGridHeight; y++)
            {
                for (var x = 0; x < CellGridWidth; x++)
                {
                    setCell(y, x, Convert.ToBoolean((int)randomizer.Next(0, 2)));
                }
            }

            Tick = 0;

        }

        /// <summary>
        /// Implementation of conway's cellular automaton.Update cell grid using readonly
        /// copy as a source and change any visual representation through a full refresh() or
        /// updateCell() which are can be redefined in the superclass.
        /// </summary>
        public void nextTick()
        {

            if (!Running)
            {   // Return if not started.
                return;
            }

            _cellMasterGrid.CopyTo(_cellGridReadOnly, 0);

            for (var y = 0; y < CellGridHeight; y++)
            {
                for (var x = 0; x < CellGridWidth; x++)
                {
                    var activeCellCount = activeCellNeighbours(y, x);
                    if (isCellActive(y, x))
                    {
                        if ((activeCellCount < 2) || (activeCellCount > 3))
                        {
                            setCell(y, x, false);
                        }
                    }
                    else
                    {
                        if (activeCellCount == 3)
                        {
                            setCell(y, x, true);
                        }
                    }
                }
            }

            refresh();

            Tick++;

        }

        /// <summary>
        /// Start automaton.
        /// </summary>
        public void start() {
            Running = true;
        }

        /// <summary>
        /// Stop automaton.
        /// </summary>
        public void stop() 
        {
            Running = false;
        }

        /// <summary>
        /// Updates a single cells state on a viewable object such as a window.
        /// </summary>
        /// <param name="y">The y coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        /// <param name="active">If set to <c>true</c> active.</param>
        public virtual void updateCell(int y, int x, bool active)
        {

        }

        /// <summary>
        /// Refresh the whole of a cell grid on a viewable object such as a window.
        /// </summary>
        public virtual void refresh()
        {

        }
    }
}
