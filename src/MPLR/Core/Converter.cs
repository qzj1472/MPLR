using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;
using System.Text;
using MPLR.Extensions;
using MPLR.Models;
using MPLR.Threading;

namespace MPLR.Core;

public sealed class Converter
{
    public async Task<bool> ExecuteAsync(string sourceFileName, string targetFormat, CancellationTokenSource? tokenSource = null)
    {
        _ = sourceFileName ?? throw new ArgumentNullException(nameof(sourceFileName));
        _ = targetFormat ?? throw new ArgumentNullException(nameof(targetFormat));

        string? recorderPath = SearchFileHelper.SearchFiles(".", "ffmpeg[\\.exe]").FirstOrDefault();

        if (recorderPath == null)
        {
            return false;
        }

        FileInfo sourceFileInfo = new(sourceFileName);

        if (!sourceFileInfo.Exists)
        {
            return false;
        }

        if (sourceFileInfo.Extension.Equals(targetFormat, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        string targetFileName = Path.ChangeExtension(sourceFileName, targetFormat);
        List<string> arguments = [];

        if (sourceFileInfo.Extension.Equals(".ts", StringComparison.CurrentCultureIgnoreCase))
        {
            arguments =
            [
                "-y",
                "-fflags", "+genpts",
                "-i", sourceFileName,
                "-c", "copy", targetFileName,
            ];
        }
        else if (sourceFileInfo.Extension.Equals(".flv", StringComparison.CurrentCultureIgnoreCase))
        {
            arguments =
            [
                "-y",
                "-i", sourceFileName,
                "-c", "copy", targetFileName,
            ];
        }
        else
        {
            return false;
        }

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = recorderPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            },
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        CancellationToken token = tokenSource?.Token ?? default;
        process.Start();
        ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);

        Task errorTask = ReadPipeAsync(process.StandardError, OnStandardErrorReceived, token);
        Task outputTask = ReadPipeAsync(process.StandardOutput, OnStandardOutputReceived, token);

        try
        {
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        await Task.WhenAll(errorTask, outputTask);

        Debug.WriteLine($"[Converter] exit code is {process.ExitCode}.");

        return process.ExitCode == 0 && File.Exists(targetFileName);
    }

    private static async Task ReadPipeAsync(StreamReader reader, Func<string, CancellationToken, Task> handler, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(token);
                if (line == null)
                {
                    break;
                }

                await handler(line, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
        }
    }

    private Task OnStandardErrorReceived(string data, CancellationToken token)
    {
        Debug.WriteLine(data);
        _ = WeakReferenceMessenger.Default.Send(new RecorderMessage()
        {
            DataType = StandardData.StandardError,
            Data = data,
        });
        return Task.CompletedTask;
    }

    private Task OnStandardOutputReceived(string data, CancellationToken token)
    {
        Debug.WriteLine(data);
        _ = WeakReferenceMessenger.Default.Send(new RecorderMessage()
        {
            DataType = StandardData.StandardOutput,
            Data = data,
        });
        return Task.CompletedTask;
    }
}

