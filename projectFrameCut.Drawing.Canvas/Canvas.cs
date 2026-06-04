using projectFrameCut.Drawing.Base;
using System.Reflection.Emit;

namespace projectFrameCut.Drawing.Canvas
{
    public class Canvas
    {
        /// <summary>
        /// Width of this canvas.
        /// </summary>
        public int Width { get; init; }
        /// <summary>
        /// Height of this canvas.
        /// </summary>
        public int Height { get; init; }

        internal List<CanvasElement> elements;

        public Canvas(int width, int height)
        {
            elements = new();
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Draw the canvas to a IPicture.
        /// </summary>
        public T Render<T>() where T : IPicture
        {
            throw new NotImplementedException();
        }

    }

    public abstract class CanvasElement
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int LayerIndex { get; set; }
        /// <summary>
        /// Draw the content to the canvas.
        /// </summary>
        public abstract T DrawBitmap<T>(int canvasWidth, int canvasHeight) where T : IPicture;
    }

    public static class CanvasExtension
    {
        extension(Canvas c)
        {
            /// <summary>
            /// Clone the specific Canvas instance with a deep copy of its CanvasElement. 
            /// </summary>
            public Canvas Clone()
            {
                var steps = new List<CanvasElement>(c.elements);
                return new Canvas(c.Width, c.Height) { elements = steps };
            }

            /// <summary>
            /// Overlay two canvas to together. Requires all canvas have same size.
            /// </summary>
            /// <exception cref="InvalidDataException">Two canvas have different size.</exception>
            public Canvas Overlay(Canvas another)
            {
                if (another.Width != c.Width || another.Height != c.Height) throw new InvalidDataException("Two canvas have different size.");
                return new Canvas(c.Width, c.Height) { elements = c.elements.Concat(another.elements).ToList() };
            }
        }
    }


}
