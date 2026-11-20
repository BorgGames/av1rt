# av1rt

A realtime AV1 encoder for .NET: a safe, Native AOT compatible C# port of libaom's realtime encoder.

```csharp
using System.Buffers;
using Av1rt;

using var y4m = Y4mReader.Open("input.y4m");
using var encoder = new Av1Encoder(new Av1EncoderOptions
{
    Width = y4m.Width,
    Height = y4m.Height,
    FrameRate = 60,
    TargetBitrateKbps = 8_000,
});
using var ivf = new IvfWriter(File.Create("output.ivf"), y4m.Width, y4m.Height, Av1Encoder.TimestampRate);
var frame = new byte[y4m.FrameSize];
var output = new ArrayBufferWriter<byte>();
while (y4m.ReadFrame(frame))
{
    output.ResetWrittenCount();
    Av1EncodeResult result = encoder.Encode(frame, output);
    if (!result.Dropped)
        ivf.WriteFrame(output.WrittenSpan, result.Timestamp);
}
```
