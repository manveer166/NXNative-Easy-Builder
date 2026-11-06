using System.Diagnostics;
using System.Text;

namespace NXInstaller.Core;

public sealed class ProcessRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(CommandSpec command, Action<string>? log, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(command.File) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = command.WorkingDirectory ?? Environment.CurrentDirectory };
        foreach (var arg in command.Args) psi.ArgumentList.Add(arg);
        if (command.Environment != null) foreach (var item in command.Environment) psi.Environment[item.Key] = item.Value;
        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var gate = new object();
        void Append(string? line)
        {
            if (line == null) return;
            lock (gate) { output.AppendLine(line); if (output.Length > 2_000_000) output.Remove(0, 500_000); }
            log?.Invoke(line);
        }
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        ct.ThrowIfCancellationRequested();
        if (!process.Start()) throw new IOException($"Cannot start {command.File}.");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var timeout = new CancellationTokenSource(command.Timeout ?? TimeSpan.FromMinutes(45));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try { await process.WaitForExitAsync(linked.Token); process.WaitForExit(); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception)
            {
                log?.Invoke("An elevated installer could not be stopped with its process tree; it may finish independently.");
                try { process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            }
            using var settle = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await process.WaitForExitAsync(settle.Token); }
            catch (InvalidOperationException) { }
            catch (OperationCanceledException) { log?.Invoke("The external process is still finishing; cancellation returned control to the installer."); }
            if (ct.IsCancellationRequested) throw;
            throw new TimeoutException($"Timed out: {Path.GetFileName(command.File)}. Open the log for details.");
        }
        return new(process.ExitCode, output.ToString());
    }
}

public static class PowerShell
{
    // No user text is interpolated into executable PowerShell syntax without literal quoting.
    public static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    public static CommandSpec Command(string script, TimeSpan? timeout = null) => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
        new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(
            "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[System.Text.UTF8Encoding]::new(); " + script)) }, Timeout: timeout);
}
