using projectFrameCut.Drawing.Text.FontHelper.Reader;

namespace projectFrameCut.Drawing.Text.FontHelper.Table;

internal static class FvarTable
{
    public static FvarData Parse(ReadOnlySpan<byte> data)
    {
        int offset = 0;

        ushort majorVersion = BigEndianReader.ReadUInt16(data, ref offset);
        ushort minorVersion = BigEndianReader.ReadUInt16(data, ref offset);
        ushort axesArrayOffset = BigEndianReader.ReadUInt16(data, ref offset);
        ushort reserved = BigEndianReader.ReadUInt16(data, ref offset);
        ushort axisCount = BigEndianReader.ReadUInt16(data, ref offset);
        ushort axisSize = BigEndianReader.ReadUInt16(data, ref offset);
        ushort instanceCount = BigEndianReader.ReadUInt16(data, ref offset);
        ushort instanceSize = BigEndianReader.ReadUInt16(data, ref offset);

        if (axisCount == 0)
            return new FvarData([], [], 0);

        // Parse axis records
        int axesOffset = axesArrayOffset;
        var axes = new VariationAxis[axisCount];
        for (int i = 0; i < axisCount; i++)
        {
            int axisStart = axesOffset + i * axisSize;
            int axisOff = axisStart;

            string tag = BigEndianReader.ReadTag(data, ref axisOff);
            float minValue = BigEndianReader.ReadFixed(data, ref axisOff);
            float defaultValue = BigEndianReader.ReadFixed(data, ref axisOff);
            float maxValue = BigEndianReader.ReadFixed(data, ref axisOff);
            ushort flags = BigEndianReader.ReadUInt16(data, ref axisOff);
            ushort axisValueNameId = BigEndianReader.ReadUInt16(data, ref axisOff);

            axes[i] = new VariationAxis(tag, minValue, defaultValue, maxValue, axisValueNameId);
        }

        // Parse named instances
        int instancesOffset = axesArrayOffset + axisCount * axisSize;
        var instances = new NamedInstance[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            int instStart = instancesOffset + i * instanceSize;
            int instOff = instStart;

            ushort subfamilyNameId = BigEndianReader.ReadUInt16(data, ref instOff);

            // Read coordinates (padding after instanceSize boundary is implicitly skipped)
            var coords = new float[axisCount];
            for (int a = 0; a < axisCount; a++)
                coords[a] = BigEndianReader.ReadFixed(data, ref instOff);

            // The subfamilyNameId references a name table entry for the display name.
            // We store the ID here; name resolution happens later (FontFace resolves it).
            // For the SubfamilyName, we'll use the NameData to look it up.
            instances[i] = new NamedInstance($"Instance_{i}", coords);
        }

        return new FvarData(axes, instances, axisCount);
    }

    /// <summary>
    /// Resolve name IDs from the fvar instances using the font's name data.
    /// Called by FontFace after both fvar and name tables are parsed.
    /// </summary>
    public static NamedInstance[] ResolveInstanceNames(
        ReadOnlySpan<byte> data, NameData nameData)
    {
        int offset = 0;

        /* major/minor */ BigEndianReader.ReadUInt16(data, ref offset);
        /* minor */ BigEndianReader.ReadUInt16(data, ref offset);
        ushort axesArrayOffset = BigEndianReader.ReadUInt16(data, ref offset);
        /* reserved */ BigEndianReader.ReadUInt16(data, ref offset);
        ushort axisCount = BigEndianReader.ReadUInt16(data, ref offset);
        ushort axisSize = BigEndianReader.ReadUInt16(data, ref offset);
        ushort instanceCount = BigEndianReader.ReadUInt16(data, ref offset);
        ushort instanceSize = BigEndianReader.ReadUInt16(data, ref offset);

        if (axisCount == 0 || instanceCount == 0)
            return [];

        int instancesOffset = axesArrayOffset + axisCount * axisSize;
        var instances = new NamedInstance[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            int instStart = instancesOffset + i * instanceSize;
            int instOff = instStart;

            ushort subfamilyNameId = BigEndianReader.ReadUInt16(data, ref instOff);

            var coords = new float[axisCount];
            for (int a = 0; a < axisCount; a++)
                coords[a] = BigEndianReader.ReadFixed(data, ref instOff);

            string? name = nameData.GetName(subfamilyNameId);
            instances[i] = new NamedInstance(name ?? $"Instance_{i}", coords);
        }

        return instances;
    }
}

internal readonly struct FvarData
{
    public VariationAxis[] Axes { get; }
    public NamedInstance[] InitialInstances { get; }
    public int AxisCount { get; }

    public FvarData(VariationAxis[] axes, NamedInstance[] instances, int axisCount)
    {
        Axes = axes;
        InitialInstances = instances;
        AxisCount = axisCount;
    }
}
