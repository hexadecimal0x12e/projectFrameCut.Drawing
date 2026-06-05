using projectFrameCut.Drawing.Base;
using System.Numerics;

namespace projectFrameCut.Drawing.Vector
{
    /// <summary>
    /// A VectorPicture represents a collection of vector elements that can be drawn on a canvas.
    /// </summary>
    public class VectorPicture
    {
        /// <summary>
        /// All the elements on the canvas. 
        /// The elements will be drawn in the order of their layer index, and the elements with the same layer index will be drawn in the order they are added to the list.
        /// </summary>
        public List<VectorCanvasElement> Elements { get; init; } = new List<VectorCanvasElement>();
    }

    public abstract class VectorCanvasElement
    {
        /// <summary>
        /// The X-axis position of the element relative to the canvas, where 0 means the left edge of the canvas and 1 means the right edge of the canvas.
        /// </summary>
        public float RelativeX { get; set; }
        /// <summary>
        /// the Y-axis position of the element relative to the canvas, where 0 means the top edge of the canvas and 1 means the bottom edge of the canvas.
        /// </summary>
        public float RelativeY { get; set; }
        /// <summary>
        /// The index of the layer this element belongs to.
        /// Elements with higher layer index will be drawn on top of elements with lower layer index.
        /// </summary>
        /// <remarks>
        /// <see cref="int.MinValue"/> means the bottom layer and <see cref="int.MaxValue"/> means the top layer.
        /// </remarks>
        public int LayerIndex { get; set; }

        /// <summary>
        /// Rotation angle in radians, applied around the element's origin.
        /// </summary>
        public float Rotation { get; set; }

        public abstract VectorSegment[] Draw();
    }

    public static class CanvasExtension
    {
        extension(VectorPicture c)
        {
            /// <summary>
            /// Clone the specific VectorPicture instance with a deep copy of its VectorCanvasElement. 
            /// </summary>
            public VectorPicture Clone()
            {
                var steps = new List<VectorCanvasElement>(c.Elements);
                return new VectorPicture { Elements = steps };
            }

            /// <summary>
            /// Overlay two canvas to together.
            /// </summary>
            /// <exception cref="InvalidDataException">Two canvas have different size.</exception>
            public VectorPicture Overlay(VectorPicture another)
            {
                return new VectorPicture { Elements = c.Elements.Concat(another.Elements).ToList() };
            }
        }
    }

}
