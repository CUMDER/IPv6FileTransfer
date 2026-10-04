using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IPv6FileTransfer.Models
{
    /// <summary>
    /// 传输任务项（用于界面显示进度）
    /// </summary>
    public class TransferItem : INotifyPropertyChanged
    {
        private string _name = "";
        private string _path = "";
        private long _size;
        private long _transferredBytes;
        private TransferStatus _status;
        private double _progress;
        private bool _isFolder;
        private string _relativePath = "";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 完整路径（发送端本地路径）
        /// </summary>
        public string Path
        {
            get => _path;
            set { _path = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 相对路径（用于接收端重建结构）
        /// </summary>
        public string RelativePath
        {
            get => _relativePath;
            set { _relativePath = value; OnPropertyChanged(); }
        }

        public long Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeText)); }
        }

        public long TransferredBytes
        {
            get => _transferredBytes;
            set
            {
                _transferredBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(TransferredText));
            }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public TransferStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public bool IsFolder
        {
            get => _isFolder;
            set { _isFolder = value; OnPropertyChanged(); }
        }

        // 格式化显示
        public string SizeText => FormatBytes(Size);
        public string TransferredText => $"{FormatBytes(TransferredBytes)} / {SizeText}";

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum TransferStatus
    {
        Pending,      // 等待中
        Transferring, // 传输中
        Completed,    // 已完成
        Failed,       // 失败
        Cancelled     // 已取消
    }
}
