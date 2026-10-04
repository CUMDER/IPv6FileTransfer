using System;
using System.Text;

namespace IPv6FileTransfer.Network
{
    /// <summary>
    /// 自定义传输协议定义
    /// </summary>
    public static class TransferProtocol
    {
        // 协议魔数（防止误连接）
        public static readonly byte[] MagicNumber = { 0x49, 0x50, 0x56, 0x36 }; // "IPV6"

        // 协议版本
        public const byte Version = 0x01;

        // 缓冲区大小
        public const int BufferSize = 64 * 1024; // 64KB
        public const int HeaderSize = 1024;      // 头部固定分配空间

        /// <summary>
        /// 消息类型（1字节）
        /// </summary>
        public enum MessageType : byte
        {
            // 握手阶段
            HandshakeRequest = 0x01,    // 发送端请求连接
            HandshakeResponse = 0x02,   // 接收端响应

            // 传输阶段
            Manifest = 0x10,            // 文件清单
            ManifestAck = 0x11,         // 清单确认
            FileStart = 0x20,           // 开始发送文件
            FileData = 0x21,            // 文件数据块
            FileEnd = 0x22,             // 单个文件结束
            FileAck = 0x23,             // 文件接收确认

            // 控制消息
            TransferComplete = 0x30,    // 全部完成
            TransferPause = 0x31,       // 暂停传输
            TransferResume = 0x32,      // 恢复传输
            TransferCancel = 0x33,      // 取消传输

            // 错误
            Error = 0xFF
        }

        /// <summary>
        /// 错误代码
        /// </summary>
        public enum ErrorCode : byte
        {
            None = 0x00,
            FileNotFound = 0x01,
            AccessDenied = 0x02,
            DiskFull = 0x03,
            InvalidManifest = 0x04,
            PathTooLong = 0x05,
            Unknown = 0xFF
        }

        /// <summary>
        /// 创建协议头部
        /// </summary>
        public static byte[] CreateHeader(MessageType type, long payloadLength)
        {
            // 格式: [Magic(4)] + [Version(1)] + [Type(1)] + [Reserved(2)] + [PayloadLength(8)]
            var header = new byte[16];

            // 魔数
            Buffer.BlockCopy(MagicNumber, 0, header, 0, 4);

            // 版本
            header[4] = Version;

            // 类型
            header[5] = (byte)type;

            // 保留字段（6-7字节，置0）

            // 数据长度（8-15字节，大端序）
            BitConverter.GetBytes(payloadLength).CopyTo(header, 8);

            return header;
        }

        /// <summary>
        /// 验证头部有效性
        /// </summary>
        public static bool ValidateHeader(byte[] header, out MessageType type, out long payloadLength)
        {
            type = 0;
            payloadLength = 0;

            if (header.Length < 16) return false;

            // 检查魔数
            for (int i = 0; i < 4; i++)
            {
                if (header[i] != MagicNumber[i]) return false;
            }

            // 检查版本
            if (header[4] != Version) return false;

            type = (MessageType)header[5];
            payloadLength = BitConverter.ToInt64(header, 8);

            return true;
        }

        /// <summary>
        /// 发送字符串（长度前缀编码）
        /// </summary>
        public static async Task WriteStringAsync(System.IO.Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await stream.WriteAsync(BitConverter.GetBytes(bytes.Length));
            await stream.WriteAsync(bytes);
        }

        /// <summary>
        /// 读取字符串
        /// </summary>
        public static async Task<string> ReadStringAsync(System.IO.Stream stream)
        {
            var lenBuffer = new byte[4];
            await stream.ReadAsync(lenBuffer, 0, 4);
            int len = BitConverter.ToInt32(lenBuffer, 0);

            var buffer = new byte[len];
            await stream.ReadAsync(buffer, 0, len);

            return Encoding.UTF8.GetString(buffer);
        }
    }
}
