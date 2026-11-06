using System.Diagnostics;
using System.Text;

namespace NXNativeEasyBuilder;

public sealed record CommandResult(int ExitCode, string Output);

public static class ProcessRunner
{
    public static async Task<CommandResult> RunAsync(string executable, IEnumerable<string> args,
        CancellationToken cancellation, Action<string>? log = null, string? folder = null,
        IReadOnlyDictionary<string, string?>? environment = null, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = folder ?? AppContext.BaseDirectory
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        if (environment != null)
            foreach (var (name, value) in environment)
                if (value == null) start.Environment.Remove(name);
                else start.Environment[name] = value;

        using var process = new Process { StartInfo = start };
        var output = new StringBuilder();
        var gate = new object();
        void Receive(string? line)
        {
            if (line == null) return;
            lock (gate)
            {
                output.AppendLine(line);
                if (output.Length > 100_000) output.Remove(0, output.Length - 80_000);
                log?.Invoke(line);
            }
        }

        cancellation.ThrowIfCancellationRequested();
        if (!process.Start()) throw new IOException($"Could not start {executable}.");
        process.StandardInput.Close();
        using var limit = new CancellationTokenSource(timeout ?? Timeout.InfiniteTimeSpan);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, cancellation);
        async Task Drain(StreamReader stream)
        {
            while (await stream.ReadLineAsync(stop.Token).ConfigureAwait(false) is { } line)
                Receive(line);
        }
        var readers = Task.WhenAll(Drain(process.StandardOutput), Drain(process.StandardError));
        try
        {
            await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
            await readers.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            try { await readers.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            cancellation.ThrowIfCancellationRequested();
            throw new TimeoutException($"{Path.GetFileName(executable)} did not respond in time.");
        }
        return new CommandResult(process.ExitCode, output.ToString());
    }
}
