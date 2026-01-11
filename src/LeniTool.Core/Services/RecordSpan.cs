namespace LeniTool.Core.Services;

public readonly record struct RecordSpan(long StartOffsetBytes, long EndOffsetBytes)
{
    public long LengthBytes => EndOffsetBytes - StartOffsetBytes;
}
