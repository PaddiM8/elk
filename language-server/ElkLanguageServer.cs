using System.Diagnostics;
using System.Text;
using Elk.LanguageServer.Rpc;
using Elk.LanguageServer.Targets;

namespace Elk.LanguageServer;
public static class ElkLanguageServer
{
    public static async Task StartAsync()
    {
        var logger = new FileLogger
        {
            LogLevel = Debugger.IsAttached || Environment.GetEnvironmentVariable("ELK_DEBUG_LSP") == "1"
                ? LogLevel.Stdio
                : LogLevel.Error,
        };

        var outStream = logger.LogLevel == LogLevel.Stdio
            ? new ProxyWriteStream(Console.OpenStandardOutput(), logger)
            : Console.OpenStandardOutput();

        var cancellationTokenSource = new CancellationTokenSource();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            cancellationTokenSource.Cancel();
        };

        var rpc = new JsonRpc(outStream, Console.OpenStandardInput(), logger);
        try
        {
            rpc.RegisterTarget(new RootTarget(cancellationTokenSource, logger));
            rpc.RegisterTarget(new TextDocumentTarget(rpc));
            await rpc.StartListeningAsync(cancellationTokenSource.Token);
            logger.LogInfo("Shutting down...");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
        }
    }
}

class ProxyWriteStream(Stream baseStream, ILogger logger) : Stream
{
    public override bool CanRead
        => false;

    public override bool CanSeek
        => false;

    public override bool CanWrite
        => true;

    public override long Length
        => baseStream.Length;

    public override long Position
    {
        get => baseStream.Position;
        set => baseStream.Position = value;
    }

    public override void Flush()
    {
        baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        baseStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        logger.LogOutput(Encoding.UTF8.GetString(buffer));
        baseStream.Write(buffer, offset, count);
    }
}