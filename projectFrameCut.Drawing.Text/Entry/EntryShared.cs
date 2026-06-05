using System;
using System.Collections.Generic;
using System.Text;

namespace projectFrameCut.Drawing.Text.Entry
{
    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    [Flags]
    public enum TextDecoration
    {
        None = 0,
        Underline = 1,
        Strikethrough = 2,
    }
}
