namespace LaserCraft.Devices;

public sealed partial class GrblMonitor
{
    // Keep one reader alive while real-time status requests bypass a pending ordinary ack.
    // Cancelling/draining this reader is required before returning ownership to the caller.
    private async Task<string> ReadJobResponseAsync(CancellationToken token)
    {
        using var silence=CancellationTokenSource.CreateLinkedTokenSource(token);
        silence.CancelAfter(TimeSpan.FromSeconds(10));
        var read=transport.ReadLineAsync(silence.Token).AsTask();
        try
        {
            while(!read.IsCompleted)
            {
                var tick=Task.Delay(TimeSpan.FromSeconds(1),silence.Token);
                if(await Task.WhenAny(read,tick)==read)break;
                await tick;
                await transport.WriteAsync("?",silence.Token);
            }
            return await read;
        }
        catch(OperationCanceledException ex)when(!token.IsCancellationRequested)
        {
            throw new IOException("No controller response for 10 seconds. Job completion is unknown; reconnect and inspect output. No commands are retried.",ex);
        }
        finally
        {
            await silence.CancelAsync();
            try{await read;}catch(Exception){ /* Preserve the originating exchange failure. */ }
        }
    }
}
