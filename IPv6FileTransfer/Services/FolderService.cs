using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IPv6FileTransfer.Models;

namespace IPv6FileTransfer.Services
{
    public class FolderService
    {
        // 生成文件夹清单（递归遍历）
        public FileManifest CreateManifest(string rootPath)
        {
            var manifest = new FileManifest
            {
                RootName = Path.GetFileName(rootPath),
                IsFolder = Directory.Exists(rootPath),
                Files = new List<FileEntry>()
            };

            if (manifest.IsFolder)
            {
                // 递归遍历文件夹
                TraverseDirectory(rootPath, rootPath, manifest.Files);
            }
            else
            {
                // 单个文件
                var fi = new FileInfo(rootPath);
                manifest.Files.Add(new FileEntry
                {
                    RelativePath = fi.Name,
                    Size = fi.Length,
                    LastModified = fi.LastWriteTimeUtc
                });
            }

            return manifest;
        }

        private void TraverseDirectory(string rootDir, string currentDir, List<FileEntry> files)
        {
            // 添加当前目录下的文件
            foreach (var file in Directory.GetFiles(currentDir))
            {
                var fi = new FileInfo(file);
                files.Add(new FileEntry
                {
                    RelativePath = Path.GetRelativePath(rootDir, file),
                    Size = fi.Length,
                    LastModified = fi.LastWriteTimeUtc
                });
            }

            // 递归子目录
            foreach (var dir in Directory.GetDirectories(currentDir))
            {
                TraverseDirectory(rootDir, dir, files);
            }
        }

        // 在接收端重建文件夹结构
        public void RebuildStructure(string targetRoot, FileManifest manifest)
        {
            string baseDir = Path.Combine(targetRoot, manifest.RootName);

            if (manifest.IsFolder)
            {
                Directory.CreateDirectory(baseDir);
            }
            else
            {
                baseDir = targetRoot; // 单文件直接放目标目录
            }

            // 预创建所有子目录（确保路径存在）
            foreach (var file in manifest.Files)
            {
                string fullPath = Path.Combine(baseDir, file.RelativePath);
                string? dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        // 序列化/反序列化清单
        public byte[] SerializeManifest(FileManifest manifest)
        {
            string json = JsonSerializer.Serialize(manifest);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public FileManifest? DeserializeManifest(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<FileManifest>(json);
        }
    }
}
