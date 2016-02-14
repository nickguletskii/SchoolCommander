/**
 * The MIT License
 * Copyright (c) 2016 Nick Guletskii
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using SchoolCommanderWPF.Model;
using SchoolCommanderWPF.Model.Core;
using SchoolCommanderWPF.Model.SendTask;
using SchoolCommanderWPF.Properties;
using SchoolCommanderWPF.Repository;
using SchoolCommanderWPF.Util;

namespace SchoolCommanderWPF.GUI {
    /// <summary>
    ///     Interaction logic for SendFilesWindow.xaml
    /// </summary>
    public partial class SendFilesWindow : Window {
        public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
            "IsRunning", typeof (bool), typeof (SendFilesWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty CopyTaskExistsProperty = DependencyProperty.Register(
            "CopyTaskExists", typeof (bool), typeof (SendFilesWindow), new PropertyMetadata(false));


        public static readonly DependencyProperty IsCancellingProperty = DependencyProperty.Register(
            "IsCancelling", typeof (bool), typeof (SendFilesWindow), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty TotalProgressProperty = DependencyProperty.Register(
            "TotalProgress", typeof (float), typeof (SendFilesWindow), new PropertyMetadata(default(float)));

        private readonly object computersLock = new object();

        private CancellationTokenSource cancellationTokenSource;

        public SendFilesWindow() {
            throw new NotSupportedException();
        }

        public SendFilesWindow(IList<Computer> selectedItems) {
            Computers = new TargetComputerManager(selectedItems.Select(c => new TargetComputer(c)));
            BindingOperations.EnableCollectionSynchronization(Computers, computersLock);
            InitializeComponent();

            ProgressTable.ItemsSource = Computers;
            FileNamesList.ItemsSource = FileNames;
        }

        public TargetComputerManager Computers { get; set; }
        public ObservableCollection<string> FileNames { get; set; } = new ObservableCollection<string>();

        public bool IsRunning {
            get { return (bool) GetValue(IsRunningProperty); }
            set { SetValue(IsRunningProperty, value); }
        }

        public float TotalProgress {
            get { return (float) GetValue(TotalProgressProperty); }
            set { SetValue(TotalProgressProperty, value); }
        }

        public bool CopyTaskExists {
            get { return (bool) GetValue(CopyTaskExistsProperty); }
            set { SetValue(CopyTaskExistsProperty, value); }
        }


        public bool IsCancelling {
            get { return (bool) GetValue(IsCancellingProperty); }
            set { SetValue(IsCancellingProperty, value); }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) {
            var fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = true;
            if (fileDialog.ShowDialog() != true)
                return;
            foreach (var path in fileDialog.FileNames) {
                FileNames.Add(path);
            }
        }

        private async Task CopyFilesAsync() {
            if (IsRunning)
                return;
            try {
                cancellationTokenSource = new CancellationTokenSource();

                Dispatcher.Invoke(
                    () => {
                        IsRunning = true;
                        CopyTaskExists = true;
                    });

                var tasks = new List<Task>();
                foreach (var computer in Computers) {
                    lock (computer.FileProgress) {
                        computer.FileProgress.Clear();
                    }
                    lock (computer.FileSize) {
                        computer.FileSize.Clear();
                    }
                    computer.JobStatus = JobStatus.Waiting;
                    tasks.AddRange(
                        from file in FileNames
                        let fileName = new FileInfo(file).Name
                        let destination =
                            string.Format(
                                "\\\\{0}\\{1}\\{2}", computer.Ip, Settings.Default.SendDestinationPath, fileName)
                        select CopyFile(computer, file, destination));
                }

                await Task.WhenAll(tasks);
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException e) {
                OnCancelled();
            }
            catch (Exception e) {
                MessageBox.Show(
                    "Operation failure: an unhandled exception occurred:" + e.Message, "Operation failure",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally {
                Dispatcher.Invoke(
                    () => {
                        IsRunning = false;
                        IsCancelling = false;
                    });
            }
        }

        private void OnCancelled() {
            foreach (var it in Computers.Select((x, i) => new {ComputerEntry = x, Index = i})) {
                if (it.ComputerEntry.JobStatus == JobStatus.Waiting ||
                    it.ComputerEntry.JobStatus == JobStatus.Copying) {
                    it.ComputerEntry.JobStatus = JobStatus.Cancelled;
                }
            }
            Dispatcher.Invoke(
                () => { IsRunning = false; });
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e) {
            await CopyFilesAsync();
        }

        private long GetTotalTransfered() {
            Func<long, long, long> plus = (x, y) => x + y;

            return
                Computers.Select(
                    c => {
                        lock (c.FileProgress) {
                            return c.FileProgress.Values.Aggregate(0l, plus);
                        }
                    }).Aggregate(0l, plus);
        }

        private long GetTotalSize() {
            Func<long, long, long> plus = (x, y) => x + y;
            return
                Computers.Select(
                    c => {
                        lock (c.FileSize) {
                            return c.FileSize.Values.Aggregate(0l, plus);
                        }
                    }).Aggregate(0l, plus);
        }

        private void RecomputeTotals() {
            var transferred = GetTotalTransfered();
            var totalSize = GetTotalSize();
            TotalProgress = (float) transferred/totalSize;
        }

        private async Task CopyFile(
            TargetComputer targetComputer, string file, string destination) {
            var progressPart = new Progress<FileProgress>(
                p => {
                    lock (targetComputer.FileSize) {
                        targetComputer.FileSize[file] = p.Total;
                    }
                    lock (targetComputer.FileProgress) {
                        targetComputer.FileProgress[file] = p.Transfered;
                    }
                    targetComputer.JobStatus = JobStatus.Copying;
                    if (p.Transfered >= p.Total) {
                        targetComputer.JobStatus = JobStatus.Success;
                    }
                    Dispatcher.InvokeAsync(RecomputeTotals);
                });
            try {
                await FileEx.CopyAsync(
                    file, destination, cancellationTokenSource.Token, progressPart);
            }
            catch (Win32Exception wex) {
                targetComputer.JobStatus = JobStatus.Failed;
                targetComputer.Exception = wex;
            }
            catch (OperationCanceledException) {
                if (targetComputer.JobStatus == JobStatus.Copying
                    || targetComputer.JobStatus == JobStatus.Waiting) {
                    targetComputer.JobStatus = JobStatus.Cancelled;
                }
                throw;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            IsCancelling = true;
            cancellationTokenSource.Cancel();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e) {
            var selected = FileNamesList.SelectedItems.Cast<object>().ToArray();
            foreach (var item in selected) FileNames.Remove((string) item);
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            if (IsRunning) {
                e.Cancel = true;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Settings.Default.Save();
        }
    }
}