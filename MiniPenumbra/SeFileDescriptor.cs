using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FileMode = FFXIVClientStructs.FFXIV.Client.System.File.FileMode;

namespace OrangeGuidanceTomestone.MiniPenumbra;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct SeFileDescriptor {
    [FieldOffset(0x00)]
    public FileMode FileMode;

    [FieldOffset(0x30)]
    public void* FileDescriptor;

    [FieldOffset(0x50)]
    public ResourceHandle* ResourceHandle;

    [FieldOffset(0x70)]
    public char Utf16FileName;
}
