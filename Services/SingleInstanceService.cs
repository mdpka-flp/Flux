using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.Services;

// Не даёт запустить второй экземпляр плеера: аргументы передаются уже запущенному окну.
// Межпроцессное взаимодействие — именованный канал (на Linux реализован через Unix-сокет).
public sealed class SingleInstanceService : IDisposable
{
    private static readonly string MutexName = BuildName("Mutex");
    private static readonly string PipeName = BuildName("Pipe");

    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _gate = new();
    private NamedPipeServerStream? _server;
    private string? _pendingPath;
    private bool _hasPending;
    private bool _disposed;

    // Вызывается в фоновом потоке при получении запроса от второго экземпляра.
    public event Action<string?>? FileRequested;

    private static string BuildName(string suffix)
    {
        // Имена не должны пересекаться у разных пользователей Linux.
        var user = Environment.UserName ?? string.Empty;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user)))[..12];
        return $"Flux.SingleInstance.{hash}.{suffix}";
    }

    // true — это первый экземпляр (нужно запускать окно), false — аргументы уже
    // переданы работающему экземпляру и процесс должен завершиться.
    public bool TryStart(string[] args)
    {
        using var mutex = new Mutex(false, MutexName);

        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(4));
            }
            catch (AbandonedMutexException)
            {
                // Предыдущий экземпляр завершился аварийно.
                acquired = true;
            }

            if (!acquired)
            {
                // Другой процесс сейчас стартует — даём ему время поднять канал.
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    if (Forward(args)) return false;
                    Thread.Sleep(100);
                }

                return false;
            }

            if (Forward(args)) return false;

            _ = Task.Run(ListenAsync);
            return true;
        }
        finally
        {
            if (acquired) mutex.ReleaseMutex();
        }
    }

    private static bool Forward(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000);

            var paths = args
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(argument =>
                {
                    try { return Path.GetFullPath(argument.Trim('"')); }
                    catch { return null; }
                })
                .Where(path => path is not null && File.Exists(path));

            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            writer.Write(string.Join('\n', paths));
            writer.Flush();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ListenAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;

            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                lock (_gate) _server = server;

                await server.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var message = await reader.ReadToEndAsync(_cancellation.Token).ConfigureAwait(false);

                var path = message
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(File.Exists);

                Dispatch(path);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_server, server)) _server = null;
                }

                server?.Dispose();
            }
        }
    }

    private void Dispatch(string? path)
    {
        lock (_gate)
        {
            if (FileRequested is null)
            {
                _pendingPath = path;
                _hasPending = true;
                return;
            }
        }

        FileRequested(path);
    }

    // Доставляет запрос, пришедший до подписки на событие.
    public void DeliverPending()
    {
        string? path;
        lock (_gate)
        {
            if (!_hasPending) return;
            path = _pendingPath;
            _pendingPath = null;
            _hasPending = false;
        }

        FileRequested?.Invoke(path);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cancellation.Cancel();

        lock (_gate)
        {
            _server?.Dispose();
            _server = null;
        }

        _cancellation.Dispose();
    }
}
