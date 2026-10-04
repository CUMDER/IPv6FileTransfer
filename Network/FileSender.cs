using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IPv6FileTransfer.Models;
using IPv6FileTransfer.Services;

namespace IPv6FileTransfer.Network
{
    public class FileSender
    {
        public event Action<double>? ProgressChanged;
        public event Action<string>? StatusChanged;

        private const int BufferSize = 64 * 1024; // 64KB 缓冲区

        public async Task SendAsync(string path, string ipv6Address, int port)
        {
            var folderService = new FolderService();
            var manifest = folderService.CreateManifest(path);

            StatusChanged?.Invoke($"正在连接 {ipv6Address}:{port}...");

            // 创建 IPv6 TCP 客户端
            using var client = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

            // 解析 IPv6 地址（支持压缩格式如 ::1, fe80::1%eth0 等）
            var ip = IPAddress.Parse(ipv6Address);
            var endpoint = new IPEndPoint(ip, port);

            await client.ConnectAsync(endpoint);
            StatusChanged?.Invoke("已连接，开始传输...");

            using var stream = new NetworkStream(client);

            // 1. 发送清单
            await SendManifestAsync(stream, manifest);

            // 2. 逐个发送文件
            long totalSent = 0;
            foreach (var fileEntry in manifest.Files)
            {
                string fullPath = manifest.IsFolder
                    ? Path.Combine(path, fileEntry.RelativePath)
                    : path;

                await SendFileAsync(stream, fullPath, fileEntry, (sent) =>
                {
                    double progress = (totalSent + sent) * 100.0 / manifest.TotalSize;
                    ProgressChanged?.Invoke(progress);
                });

                totalSent += fileEntry.Size;
            }

            // 3. 发送完成标记
            await stream.WriteAsync(new[] { (byte)MessageType.TransferComplete });

            StatusChanged?.Invoke("传输完成！");
        }

        private async Task SendManifestAsync(NetworkStream stream, FileManifest manifest)
        {
            var folderService = new FolderService();
            byte[] manifestData = folderService.SerializeManifest(manifest);

            // 发送类型 + 长度 + 数据
            await stream.WriteAsync(new[] { (byte)MessageType.Manifest });
            await stream.WriteAsync(BitConverter.GetBytes(manifestData.Length));
            await stream.WriteAsync(manifestData);

            // 等待接收端确认
            var ackBuffer = new byte[1];
            await stream.ReadAsync(ackBuffer, 0, 1);
            if (ackBuffer[0] != 1) throw new Exception("接收端拒绝清单");
        }

        private async Task SendFileAsync(NetworkStream stream, string fullPath, FileEntry entry, Action<long> onProgress)
        {
            // 发送文件头
            await stream.WriteAsync(new[] { (byte)MessageType.FileStart });
            byte[] pathBytes = Encoding.UTF8.GetBytes(entry.RelativePath);
            await stream.WriteAsync(BitConverter.GetBytes(pathBytes.Length));
            await stream.WriteAsync(pathBytes);
            await stream.WriteAsync(BitConverter.GetBytes(entry.Size));

            // 发送文件内容
            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
            var buffer = new byte[BufferSize];
            long sent = 0;
            int read;

            while ((read = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, read);
                sent += read;
                onProgress(sent);
            }

            // 发送结束标记
            await stream.WriteAsync(new[] { (byte)MessageType.FileEnd });
        }
    }
}
