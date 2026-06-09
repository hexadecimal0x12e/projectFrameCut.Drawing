using System;
using System.Collections.Generic;
using System.Text;

namespace projectFrameCut.Drawing.Text.Entry
{
    /// <summary>Horizontal text alignment within the layout box.</summary>
    public enum TextAlignment
    {
        /// <summary>Align text to the left edge.</summary>
        Left,
        /// <summary>Center text horizontally.</summary>
        Center,
        /// <summary>Align text to the right edge.</summary>
        Right
    }

    /// <summary>Text decoration flags for underlining and strikethrough.</summary>
    [Flags]
    public enum TextDecoration
    {
        /// <summary>No decoration.</summary>
        None = 0,
        /// <summary>Draw a line under the text.</summary>
        Underline = 1,
        /// <summary>Draw a line through the text.</summary>
        Strikethrough = 2,
    }

    /// <summary>Text flow direction for rendering.</summary>
    public enum TextFlowDirection
    {
        /// <summary>Left-to-right text flow.</summary>
        LeftToRight = 0,
        /// <summary>Right-to-left text flow.</summary>
        RightToLeft = 1,
    }
}
