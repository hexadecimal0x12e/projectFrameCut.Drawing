namespace projectFrameCut.Drawing.Processing.Composing
{
    /// <summary>Blend modes for composing two pictures.</summary>
    public enum BlendMode
    {
        /// <summary>Standard alpha blending.</summary>
        Overlay,
        /// <summary>Adds pixel values (clamped to maximum).</summary>
        Add,
        /// <summary>Subtracts top pixel from base pixel.</summary>
        Subtract,
        /// <summary>Multiplies pixel values.</summary>
        Multiply,
        /// <summary>Screen blend (inverse multiply).</summary>
        Screen,
        /// <summary>Overlay blend (multiply or screen based on base value).</summary>
        OverlayBlend,
        /// <summary>Keeps the darker of the two pixels.</summary>
        Darken,
        /// <summary>Keeps the lighter of the two pixels.</summary>
        Lighten,
        /// <summary>Absolute difference between pixels.</summary>
        Difference,
    }
}
