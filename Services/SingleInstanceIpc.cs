using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Panak.Services {
    public class SingleInstanceIpc : IDisposable {
        private const string PIPE_NAME = "PanakIpcNamedPipe_Local";
        private CancellationTokenSource? _cts;
        public event Action<string>? CommandReceived;

        public static bool SendCommand(string command) {
            try {
                using var client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
                client.Connect(500); // 500ms timeout
                byte[] data = Encoding.UTF8.GetBytes(command);
                client.Write(data, 0, data.Length);
                client.Flush();
                return true;
            } catch {
                return false;
            }
        }

        public void StartServer() {
            _cts = new CancellationTokenSource();
            Task.Run(() => ServerLoop(_cts.Token));
        }

        private async Task ServerLoop(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    using var server = new NamedPipeServerStream(
                        PIPE_NAME,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? line = await reader.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(line)) {
                        CommandReceived?.Invoke(line.Trim());
                    }
                } catch (OperationCanceledException) {
                    break;
                } catch {
                    // Small delay to prevent tight loop on pipe error
                    await Task.Delay(200, token);
                }
            }
        }

        public void Dispose() {
            try {
                _cts?.Cancel();
            } catch { }
            try {
                _cts?.Dispose();
            } catch { }
            _cts = null;
        }
    }
}
