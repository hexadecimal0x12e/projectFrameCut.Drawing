namespace projectFrameCut.Drawing.Vector.ImportExport
{
    /// <summary>抗锯齿模式，控制 VectorToIPicture 渲染时的边缘平滑方式。</summary>
    public enum AntiAliasMode
    {
        /// <summary>无抗锯齿（默认）。使用二进制覆盖判断，性能最高。</summary>
        None = 0,

        /// <summary>
        /// 超级采样抗锯齿 2 倍。以 2 倍宽高（4 倍像素量）内部渲染后下采样至目标尺寸。
        /// 所有渲染器自动获得平滑效果，推荐日常使用。
        /// </summary>
        SSAA2x = 1,

        /// <summary>
        /// 超级采样抗锯齿 4 倍。以 4 倍宽高（16 倍像素量）内部渲染后下采样至目标尺寸。
        /// 画质最好，但内存和渲染时间最高。
        /// </summary>
        SSAA4x = 2,
    }
}
