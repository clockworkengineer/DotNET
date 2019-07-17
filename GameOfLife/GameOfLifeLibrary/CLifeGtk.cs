using System;
using Gdk;
using Cairo;

namespace GameOfLifeLibrary
{
    public class CLifeGtk : CLife
    {
      
        Context _cellGridContext;
        int _scaleFactor;

        public CLifeGtk(Gdk.Window cellWindow, int height, int width, int scaleFactor) : base(height,width)
        {

            _scaleFactor = scaleFactor;
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
            Cairo.Color black = new Cairo.Color(0, 0, 0);
            Cairo.Color white = new Cairo.Color(0.95, 0.95, 0.95);

            for (var y = 0; y < CellGridHeight; y++)
            {
                for (var x = 0; x < CellGridWidth; x++)
                {
                    if (getCell(y, x))
                    {
                        _cellGridContext.SetSourceColor(black);
                    }
                    else
                    {
                        _cellGridContext.SetSourceColor(white);
                    }

                    _cellGridContext.Rectangle(new Cairo.Rectangle(x*_scaleFactor, y*_scaleFactor, _scaleFactor, _scaleFactor));
                    _cellGridContext.Fill();
                }
            }
        }
    }
}