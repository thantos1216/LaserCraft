namespace LaserCraft.Devices;
internal sealed class SerializedGrblTransport(IGrblTransport inner) : IGrblTransport
{
    private int disposed;
    private readonly SemaphoreSlim writes = new(1);
    public async ValueTask WriteAsync(string command, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        await writes.WaitAsync(token); try { ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);token.ThrowIfCancellationRequested(); await inner.WriteAsync(command,token); } finally { writes.Release(); }
    }
    public ValueTask<string> ReadLineAsync(CancellationToken token) => inner.ReadLineAsync(token);
    public async ValueTask DisposeAsync() { if(Interlocked.Exchange(ref disposed,1)!=0)return;await writes.WaitAsync();try{await inner.DisposeAsync();}finally{writes.Release();} }
}
