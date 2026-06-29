using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;
using System.Text;
using MPLR.Extensions;
using MPLR.Models;
using MPLR.Threading;

namespace MPLR.Core;

public sealed class Converter
{
    private static int activeCount;

    public static bool HasActiveConversions => Volatile.Read(ref activeCount) > 0;

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
        List<string> arguments = sourceFileInfo.Extension.ToLowerInvariant() switch
        {
            ".ts" => CreateCopyArguments(sourceFileName, targetFileName, true),
            ".flv" => CreateCopyArguments(sourceFileName, targetFileName, false),
            _ => []
        };

        if (arguments.Count == 0)
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
        Interlocked.Increment(ref activeCount);
        try
        {
            process.Start();
            ChildProcessTracerPeriodicTimer.Default.TryTraceProcess(process);

            Task errorTask = ReadPipeAsync(process.StandardError, OnStandardErrorReceived, token);
            Task outputTask = ReadPipeAsync(process.StandardOutput, OnStandardOutputReceived, token);

            await process.WaitForExitAsync(token);

            await Task.WhenAll(errorTask, outputTask);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref activeCount);
        }

        Debug.WriteLine($"[Converter] exit code is {process.ExitCode}.");

        return process.ExitCode == 0 && File.Exists(targetFileName);
    }

    private static List<string> CreateCopyArguments(string sourceFileName, string targetFileName, bool useGenPts)
    {
        List<string> arguments =
        [
            "-y",
        ];

        if (useGenPts)
        {
            arguments.AddRange(["-fflags", "+genpts"]);
        }

        arguments.AddRange([
            "-i", sourceFileName,
            "-map", "0:v?",
            "-map", "0:a?",
            "-map", "0:s?",
            "-map_metadata", "0",
            "-map_chapters", "0",
            "-c", "copy",
            targetFileName,
        ]);

        return arguments;
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

