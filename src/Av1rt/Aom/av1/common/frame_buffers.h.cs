// Port of libaom av1/common/frame_buffers.h

namespace Av1rt.Aom;

internal partial struct InternalFrameBuffer
{
    public Ptr<byte> data;
    public int size;
    public int in_use;
}

internal partial struct InternalFrameBufferList
{
    public int num_internal_frame_buffers;
    public Ptr<InternalFrameBuffer> int_fb;
}
