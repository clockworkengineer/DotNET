using System;
using Gdk;
using Cairo;

namespace GameOfLifeLibrary
{
    public class CLifeGtk : CLife
    {
      
        private Context _cellGridContext;
        private int _scaleFactor=1;
        private Cairo.Color _inactiveCellColor = new Cairo.Color(0.95, 0.95, 0.95);
        private Cairo.Color _activeCellColor = new Cairo.Color(0, 0, 0);

        public int ScaleFactor { get => _scaleFactor; set => _scaleFactor = value; }
        public Cairo.Color InactiveCellColor { get => _inactiveCellColor; set => _inactiveCellColor = value; }
        public Cairo.Color ActiveCellColor{ get => _activeCellColor; set => _activeCellColor = value; }

        public CLifeGtk(Gdk.Window cellWindow, int height, int width) : base(height,width)
        {

            _cellGridContext = Gdk.CairoHelper.Create(cellWindow);
            _cellGridContext.Antialias = Cairo.Antialias.None;
            _cellGridContext.Rotate(0);
            _cellGridContext.Translate(0, 0);
            _cellGridContext.LineWidth = 1;
            
        }

        ~CLifeGtk()
        {
            _cellGridContext.GetTarget().Dispose();
            _cellGridContext.Dispose();
        }

        public override void refresh()
        {

            if (Running) {
                clearDrawArea();
            }

            for (var y = 0; y < CellGridHeight; y++)
            {
                for (var x = 0; x < CellGridWidth; x++)
                {
                    if (getCell(y, x))
                    {
                        _cellGridContext.SetSourceColor(ActiveCellColor);
                        _cellGridContext.Rectangle(new Cairo.Rectangle(x * ScaleFactor, y * ScaleFactor, ScaleFactor, ScaleFactor));
                        _cellGridContext.Fill();
                    }
                }
            }
        }

        public void clearDrawArea() {
            _cellGridContext.SetSourceColor(InactiveCellColor);
            _cellGridContext.Rectangle(new Cairo.Rectangle(0, 0, CellGridWidth * ScaleFactor, CellGridHeight * ScaleFactor));
            _cellGridContext.Fill();
        }

    }
}