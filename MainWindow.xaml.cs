using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace IPv6FileTransfer
{
    public partial class MainWindow : Window
    {
        private List<string> _droppedPaths = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
        }


        // 拖入时高亮显示
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        // 处理拖放
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (rbSend.IsChecked == false) return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            _droppedPaths.Clear();
            _droppedPaths.AddRange(files);

            // 显示拖入的内容
            lstFiles.Items.Clear();
            foreach (var path in files)
            {
                string type = Directory.Exists(path) ? "[文件夹]" : "[文件]";
                lstFiles.Items.Add($"{type} {Path.GetFileName(path)}");
            }
            lstFiles.Visibility = Visibility.Visible;
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {


            btnStart.IsEnabled = false;
            try
            {
                if (rbSend.IsChecked == true)
                {
                    if (_droppedPaths.Count == 0)
                    {
                        MessageBox.Show("请先拖入文件或文件夹");
                        btnStart.IsEnabled = false;
                        return;
                    }
                    // 发送模式
                    var fileSender = new Network.FileSender();
                    fileSender.ProgressChanged += (p) => Dispatcher.Invoke(() => progressBar.Value = p);

                    foreach (var path in _droppedPaths)
                    {
                        await fileSender.SendAsync(path, txtTargetIP.Text, int.Parse(txtPort.Text));
                    }
                }
                else
                {
                    // 接收模式（在后台监听）
                    var receiver = new Network.FileReceiver();
                    receiver.ProgressChanged += (p) => Dispatcher.Invoke(() => progressBar.Value = p);
                    receiver.StatusChanged += (msg) => Dispatcher.Invoke(() => Title = msg);

                    await receiver.StartListeningAsync(int.Parse(txtPort.Text));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"错误: {ex.Message}");
            }
            finally
            {
                btnStart.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = true;
            // 取消逻辑
            _droppedPaths.Clear();
            lstFiles.Visibility = Visibility.Collapsed;
            progressBar.Value = 0;
        }
    }
}
