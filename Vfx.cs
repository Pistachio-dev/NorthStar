using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using OrangeGuidanceTomestone.Util;

namespace OrangeGuidanceTomestone;

internal unsafe class Vfx : IDisposable {
    private static readonly byte[] Pool = "Client.System.Scheduler.Instance.VfxObject\0"u8.ToArray();

    [Signature("E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08")]
    private readonly delegate* unmanaged<byte*, byte*, VfxStruct*> _staticVfxCreate;

    [Signature("E8 ?? ?? ?? ?? B0 ?? EB ?? B0 ?? 88 83")]
    private readonly delegate* unmanaged<VfxStruct*, float, int, ulong> _staticVfxRun;

    [Signature("40 53 48 83 EC 20 48 8B D9 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 28 33 D2 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9")]
    private readonly delegate* unmanaged<VfxStruct*, nint> _staticVfxRemove;

    private Plugin Plugin { get; }
    internal SemaphoreSlim Mutex { get; } = new(1, 1);
    internal Dictionary<Guid, nint> Spawned { get; } = [];
    private Queue<IQueueAction> Queue { get; } = [];
    private bool _disposed;
    private readonly Stopwatch _queueTimer = Stopwatch.StartNew();

    internal Vfx(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.GameInteropProvider.InitializeFromAttributes(this);
        this.Plugin.Framework.Update += this.HandleQueues;
    }

    public void Dispose() {
        if (this._disposed) {
            return;
        }

        this._disposed = true;
        this.Plugin.Framework.Update -= this.HandleQueues;
        this.RemoveAllSync();
    }

    private void HandleQueues(IFramework framework) {
        this._queueTimer.Restart();

        while (this._queueTimer.Elapsed < TimeSpan.FromMilliseconds(1)) {
            if (!this.Queue.TryDequeue(out var action)) {
                return;
            }

            switch (action) {
                case AddQueueAction add: {
                    using var guard = this.Mutex.With();
                    Plugin.Log.Debug($"adding vfx for {add.Id}");
                    if (this.Spawned.Remove(add.Id, out var existing)) {
                        Plugin.Log.Warning($"vfx for {add.Id} already exists, queuing remove");
                        this.Queue.Enqueue(new RemoveRawQueueAction(existing));
                    }

                    var vfx = this.SpawnStatic(add.Path, add.Position, add.Rotation);
                    this.Spawned[add.Id] = (nint) vfx;
                    break;
                }

                case RemoveQueueAction remove: {
                    using var guard = this.Mutex.With();
                    Plugin.Log.Debug($"removing vfx for {remove.Id}");
                    if (!this.Spawned.Remove(remove.Id, out var ptr)) {
                        break;
                    }

                    this.RemoveStatic((VfxStruct*) ptr);
                    break;
                }
                    ;

                case RemoveRawQueueAction remove: {
                    Plugin.Log.Debug($"removing raw vfx at {remove.Pointer:X}");
                    this.RemoveStatic((VfxStruct*) remove.Pointer);
                    break;
                }
            }
        }
    }

    internal void RemoveAllSync() {
        using var guard = this.Mutex.With();

        foreach (var spawned in this.Spawned.Values.ToArray()) {
            this.RemoveStatic((VfxStruct*) spawned);
        }

        this.Spawned.Clear();
    }

    internal void QueueSpawn(Guid id, string path, Vector3 pos, Quaternion rotation) {
        using var guard = this.Mutex.With();
        this.Queue.Enqueue(new AddQueueAction(id, path, pos, rotation));
    }

    internal void QueueRemove(Guid id) {
        using var guard = this.Mutex.With();
        this.Queue.Enqueue(new RemoveQueueAction(id));
    }

    internal void QueueRemoveAll() {
        using var guard = this.Mutex.With();

        foreach (var id in this.Spawned.Keys) {
            this.Queue.Enqueue(new RemoveQueueAction(id));
        }
    }

    private VfxStruct* SpawnStatic(string path, Vector3 pos, Quaternion rotation) {
        VfxStruct* vfx;
        fixed (byte* p = Encoding.UTF8.GetBytes(path).NullTerminate()) {
            fixed (byte* pool = Pool) {
                vfx = this._staticVfxCreate(p, pool);
            }
        }

        if (vfx == null) {
            return null;
        }

        // update position
        vfx->Position = new Vector3(pos.X, pos.Y, pos.Z);
        // update rotation
        vfx->Rotation = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);

        // set alpha and colours from config
        vfx->Red = Math.Clamp(this.Plugin.Config.SignRed / 100, 0, 1);
        vfx->Green = Math.Clamp(this.Plugin.Config.SignGreen / 100, 0, 1);
        vfx->Blue = Math.Clamp(this.Plugin.Config.SignBlue / 100, 0, 1);
        vfx->Alpha = Math.Clamp(this.Plugin.Config.SignAlpha / 100, 0, 1);

        // remove flag that sometimes causes vfx to not appear?
        vfx->SomeFlags &= 0xF7;

        // update
        vfx->Flags |= 2;

        this._staticVfxRun(vfx, 0.0f, -1);

        return vfx;
    }

    private void RemoveStatic(VfxStruct* vfx) {
        this._staticVfxRemove(vfx);
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct VfxStruct {
        [FieldOffset(0x38)]
        public byte Flags;

        [FieldOffset(0x50)]
        public Vector3 Position;

        [FieldOffset(0x60)]
        public Quaternion Rotation;

        [FieldOffset(0x70)]
        public Vector3 Scale;

        [FieldOffset(0x128)]
        public int ActorCaster;

        [FieldOffset(0x130)]
        public int ActorTarget;

        [FieldOffset(0x1B8)]
        public int StaticCaster;

        [FieldOffset(0x1C0)]
        public int StaticTarget;

        [FieldOffset(0x248)]
        public byte SomeFlags;

        [FieldOffset(0x260)]
        public float Red;

        [FieldOffset(0x264)]
        public float Green;

        [FieldOffset(0x268)]
        public float Blue;

        [FieldOffset(0x26C)]
        public float Alpha;
    }
}

internal interface IQueueAction;

internal sealed record AddQueueAction(
    Guid Id,
    string Path,
    Vector3 Position,
    Quaternion Rotation
) : IQueueAction;

internal sealed record RemoveQueueAction(Guid Id) : IQueueAction;

internal sealed record RemoveRawQueueAction(nint Pointer) : IQueueAction;
