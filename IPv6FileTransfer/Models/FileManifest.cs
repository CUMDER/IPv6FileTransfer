using System;
using System.Collections.Generic;

namespace IPv6FileTransfer.Models
{
    public class FileManifest
    {
        public string RootName { get; set; } = "";
        public bool IsFolder { get; set; }
        public List<FileEntry> Files { get; set; } = new();
        public int TotalFiles => Files.Count;
        public long TotalSize => Files.Sum(f => f.Size);
    }

    public class FileEntry
    {
        public string RelativePath { get; set; } = "";  // 相对路径（如 "src/main.cs"）
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? Hash { get; set; }  // 可选：MD5/SHA256 校验
    }

    // 协议定义
    public enum MessageType : byte
    {
        Manifest = 1,      // 发送清单
        FileStart = 2,     // 开始发送文件
        FileData = 3,      // 文件数据块
        FileEnd = 4,       // 文件结束
        TransferComplete = 5, // 全部完成
        Error = 255
    }
}
