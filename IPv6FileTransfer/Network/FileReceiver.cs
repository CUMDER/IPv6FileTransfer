using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using IPv6FileTransfer.Models;
using IPv6FileTransfer.Services;

namespace IPv6FileTransfer.Network
{
    public class FileReceiver
    {
        public event Action<double>? ProgressChanged;
        public event Action<string>? StatusChanged;

        private const int BufferSize = 64 * 1024;
        private string _saveDirectory;

        public FileReceiver(string saveDirectory = "./Received")
        {
            _saveDirectory = saveDirectory;
            Directory.CreateDirectory(saveDirectory);
        }

        public async Task StartListeningAsync(int port)
        {
            // 创建 IPv6 监听器，支持双栈 IPv4 映射地址
            var listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

            // 启用双栈模式（如果系统支持）
            try
            {
                listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            }
            catch { /* 某些系统不支持双栈 */ }

            var endpoint = new IPEndPoint(IPAddress.IPv6Any, port);
            listener.Bind(endpoint);
            listener.Listen(10);

            StatusChanged?.Invoke($"正在监听端口 {port}...");

            while (true)
            {
                var client = await listener.AcceptAsync();
                _ = HandleClientAsync(client); // 异步处理，不阻塞接收下一个
            }
        }

        private async Task HandleClientAsync(Socket client)
        {
            using (client)
            using (var stream = new NetworkStream(client))
            {
                try
                {
                    var folderService = new FolderService();

                    // 1. 接收清单
                    var manifest = await ReceiveManifestAsync(stream);
                    StatusChanged?.Invoke($"接收: {manifest.RootName} ({manifest.TotalFiles} 个文件)");

                    // 重建目录结构
                    folderService.RebuildStructure(_saveDirectory, manifest);
                    string baseDir = manifest.IsFolder
                        ? Path.Combine(_saveDirectory, manifest.RootName)
                        : _saveDirectory;

                    // 2. 接收文件
                    long totalReceived = 0;
                    for (int i = 0; i < manifest.TotalFiles; i++)
                    {
                        var entry = await ReceiveFileHeaderAsync(stream);
                        string savePath = Path.Combine(baseDir, entry.RelativePath);

                        await ReceiveFileDataAsync(stream, savePath, entry.Size, (received) =>
                        {
                            double progress = (totalReceived + received) * 100.0 / manifest.TotalSize;
                            ProgressChanged?.Invoke(progress);
                        });

                        totalReceived += entry.Size;
                        StatusChanged?.Invoke($"已接收: {entry.RelativePath}");
                    }

                    StatusChanged?.Invoke("全部接收完成！");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"错误: {ex.Message}");
                }
            }
        }

        private async Task<FileManifest> ReceiveManifestAsync(NetworkStream stream)
        {
            var typeBuffer = new byte[1];
            await stream.ReadAsync(typeBuffer, 0, 1);
            if (typeBuffer[0] != (byte)MessageType.Manifest)
                throw new Exception("协议错误：期望清单");

            var lenBuffer = new byte[4];
            await stream.ReadAsync(lenBuffer, 0, 4);
            int len = BitConverter.ToInt32(lenBuffer, 0);

            var manifestData = new byte[len];
            await stream.ReadAsync(manifestData, 0, len);

            // 发送确认
            await stream.WriteAsync(new[] { (byte)1 });

            var folderService = new FolderService();
            return folderService.DeserializeManifest(manifestData)
                ?? throw new Exception("清单解析失败");
        }

        private async Task<FileEntry> ReceiveFileHeaderAsync(NetworkStream stream)
        {
            var typeBuffer = new byte[1];
            await stream.ReadAsync(typeBuffer, 0, 1);
            if (typeBuffer[0] != (byte)MessageType.FileStart)
                throw new Exception("协议错误：期望文件头");

            // 读取路径
            var lenBuffer = new byte[4];
            await stream.ReadAsync(lenBuffer, 0, 4);
            int pathLen = BitConverter.ToInt32(lenBuffer, 0);
            var pathBuffer = new byte[pathLen];
            await stream.ReadAsync(pathBuffer, 0, pathLen);
            string relativePath = Encoding.UTF8.GetString(pathBuffer);

            // 读取大小
            var sizeBuffer = new byte[8];
            await stream.ReadAsync(sizeBuffer, 0, 8);
            long size = BitConverter.ToInt64(sizeBuffer, 0);

            return new FileEntry { RelativePath = relativePath, Size = size };
        }

        private async Task ReceiveFileDataAsync(NetworkStream stream, string savePath, long size, Action<long> onProgress)
        {
            using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
            var buffer = new byte[BufferSize];
            long received = 0;

            while (received < size)
            {
                int toRead = (int)Math.Min(buffer.Length, size - received);
                int read = await stream.ReadAsync(buffer, 0, toRead);
                if (read == 0) throw new Exception("连接中断");

                await fs.WriteAsync(buffer, 0, read);
                received += read;
                onProgress(received);
            }

            // 验证结束标记
            var endBuffer = new byte[1];
            await stream.ReadAsync(endBuffer, 0, 1);
            if (endBuffer[0] != (byte)MessageType.FileEnd)
                throw new Exception("文件传输不完整");
        }
    }
}
