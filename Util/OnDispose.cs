namespace OrangeGuidanceTomestone.Util;

internal class OnDispose : IDisposable {
    private bool _disposed;
    private readonly Action _action;

    internal OnDispose(Action action) {
        this._action = action;
    }

    public void Dispose() {
        if (this._disposed) {
            return;
        }

        this._disposed = true;
        this._action();
    }
}
