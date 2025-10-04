using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FileMode = FFXIVClientStructs.FFXIV.Client.System.File.FileMode;

namespace NorthStar.MiniPenumbra;

internal unsafe class VfxReplacer : IDisposable
{
    private delegate byte ReadSqPackDelegate(void* resourceManager, SeFileDescriptor* pFileDesc, int priority, bool isSync);

    [Signature("40 56 41 56 48 83 EC 28 0F BE 02", DetourName = nameof(ReadSqPackDetour))]
    private Hook<ReadSqPackDelegate> _readSqPackHook;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 63 42 28")]
    private delegate* unmanaged<void*, SeFileDescriptor*, int, bool, byte> _readFile;

    private Plugin Plugin { get; }

    internal VfxReplacer(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        _readSqPackHook!.Enable();
    }

    public void Dispose()
    {
        _readSqPackHook.Dispose();
    }

    private byte ReadSqPackDetour(void* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        try
        {
            return ReadSqPackDetourInner(resourceManager, fileDescriptor, priority, isSync);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in ReadSqPackDetour");
            return _readSqPackHook.Original(resourceManager, fileDescriptor, priority, isSync);
        }
    }

    private byte ReadSqPackDetourInner(void* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        if (!Plugin.Config.RemoveGlow)
        {
            goto Original;
        }

        if (fileDescriptor == null || fileDescriptor->ResourceHandle == null)
        {
            goto Original;
        }

        var fileName = fileDescriptor->ResourceHandle->FileName;
        if (fileName.BasicString.First == null)
        {
            goto Original;
        }

        if (Plugin.VfxSpawner.IsTerritoryWithOriginalEffects())
        {
            goto Original;
        }

        var path = fileName.ToString();
        
        if (path == "bg/ex2/02_est_e3/common/vfx/eff/b0941trp1f_o.avfx")
        {
            Plugin.Log.Warning($"Replacing VFX with path {path} with a light pillar");
            var p = Path.Join(Plugin.AvfxFilePath, $"PillarOfLight_groundTarget.avfx");

            return DefaultRootedResourceLoad(p, resourceManager, fileDescriptor, priority, isSync);
        }
        var index = Array.IndexOf(Messages.VfxPaths, path);
        if (index == -1)
        {
            goto Original;
        }

        var letter = (char)('a' + index);
        var newPath = Path.Join(Plugin.AvfxFilePath, $"sign_{letter}.avfx");
        return DefaultRootedResourceLoad(newPath, resourceManager, fileDescriptor, priority, isSync);

    Original:
        return _readSqPackHook.Original(resourceManager, fileDescriptor, priority, isSync);
    }

    // Load the resource from a path on the users hard drives.
    private byte DefaultRootedResourceLoad(string gamePath, void* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        // Specify that we are loading unpacked files from the drive.
        // We need to copy the actual file path in UTF16 (Windows-Unicode) on two locations,
        // but since we only allow ASCII in the game paths, this is just a matter of upcasting.
        fileDescriptor->FileMode = FileMode.LoadUnpackedResource;

        var fd = stackalloc byte[0x20 + 2 * gamePath.Length + 0x16];
        fileDescriptor->FileDescriptor = fd;
        var fdPtr = (char*)(fd + 0x21);
        for (var i = 0; i < gamePath.Length; ++i)
        {
            (&fileDescriptor->Utf16FileName)[i] = gamePath[i];
            fdPtr[i] = gamePath[i];
        }

        (&fileDescriptor->Utf16FileName)[gamePath.Length] = '\0';
        fdPtr[gamePath.Length] = '\0';

        // Use the SE ReadFile function.
        var ret = _readFile(resourceManager, fileDescriptor, priority, isSync);
        return ret;
    }
}