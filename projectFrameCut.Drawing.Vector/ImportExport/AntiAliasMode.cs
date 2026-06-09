namespace projectFrameCut.Drawing.Vector.ImportExport
{
    /// <summary>Anti-aliasing mode for vector-to-raster conversion.</summary>
    public enum AntiAliasMode
    {
        /// <summary>No anti-aliasing.</summary>
        None = 0,
        /// <summary>2x supersampling anti-aliasing.</summary>
        SSAA2x = 2,
        /// <summary>4x supersampling anti-aliasing.</summary>
        SSAA4x = 4,
        /// <summary>8x supersampling anti-aliasing.</summary>
        SSAA8x = 8,
    }
}
