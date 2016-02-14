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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using SchoolCommanderWPF.Model;
using SchoolCommanderWPF.Model.CollectTask;
using SchoolCommanderWPF.Model.Core;
using SchoolCommanderWPF.Properties;
using SchoolCommanderWPF.Repository;
using SchoolCommanderWPF.Util;
using MessageBox = System.Windows.MessageBox;

namespace SchoolCommanderWPF.GUI {
    /// <summary>
    ///     Interaction logic for CollectFilesWindow.xaml
    /// </summary>
    public partial class CollectFilesWindow : Window {
        public enum ShowCommands {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11
        }

        private readonly object computersLock = new object();
        private readonly object previewTreeLock = new object();

        private CancellationTokenSource cancellationTokenSource;

        public CollectFilesWindow() {
            throw new NotSupportedException();
        }

        public CollectFilesWindow(IList<Computer> selectedItems) {
            Computers = new SourceComputerManager(selectedItems.Select(c => new SourceComputer(c)));
            BindingOperations.EnableCollectionSynchronization(Computers, computersLock);
            BindingOperations.EnableCollectionSynchronization(PreviewTreeItems, previewTreeLock);
            InitializeComponent();

            ProgressTable.ItemsSource = Computers;
            FilePreviewTreeView.ItemsSource = PreviewTreeItems;
        }

        public static readonly DependencyProperty IsDestinationBrowseDialogOpenProperty = DependencyProperty.Register(
            "IsDestinationBrowseDialogOpen", typeof (bool), typeof (CollectFilesWindow), new PropertyMetadata(false));

        public bool IsDestinationBrowseDialogOpen {
            get { return (bool) GetValue(IsDestinationBrowseDialogOpenProperty); }
            set { SetValue(IsDestinationBrowseDialogOpenProperty, value); }
        }

        public static readonly DependencyProperty IsPreviewComputingProperty = DependencyProperty.Register(
            "IsPreviewComputing", typeof (bool), typeof (CollectFilesWindow), new PropertyMetadata(false));

        public bool IsPreviewComputing {
            get { return (bool) GetValue(IsPreviewComputingProperty); }
            set { SetValue(IsPreviewComputingProperty, value); }
        }
        
        public SourceComputerManager Computers { get; }
        public ObservableCollection<string> FileNames { get; set; } = new ObservableCollection<string>();

        public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
            "IsRunning", typeof (bool), typeof (CollectFilesWindow), new PropertyMetadata(false));

        public bool IsRunning {
            get { return (bool) GetValue(IsRunningProperty); }
            set { SetValue(IsRunningProperty, value); }
        }

        public static readonly DependencyProperty TotalProgressProperty = DependencyProperty.Register(
            "TotalProgress", typeof (float), typeof (CollectFilesWindow), new PropertyMetadata(0.0f));

        public float TotalProgress {
            get { return (float) GetValue(TotalProgressProperty); }
            set { SetValue(TotalProgressProperty, value); }
        }

        public static readonly DependencyProperty IsCancellingProperty = DependencyProperty.Register(
            "IsCancelling", typeof (bool), typeof (CollectFilesWindow), new PropertyMetadata(default(bool)));

        public bool IsCancelling {
            get { return (bool) GetValue(IsCancellingProperty); }
            set { SetValue(IsCancellingProperty, value); }
        }
        public ObservableCollection<CollectPreviewComputerEntry> PreviewTreeItems { get; } =
            new ObservableCollection<CollectPreviewComputerEntry>();


        private async Task CopyFilesAsync() {
            if (IsRunning)
                return;
            try {
                cancellationTokenSource = new CancellationTokenSource();

                Dispatcher.Invoke(()=>IsRunning =true);
                var tasks = new List<Task>();
                foreach (var computer in Computers) {
                    lock (computer.FileProgress) {
                        computer.FileProgress.Clear();
                    }
                    lock (computer.FileSize) {
                        computer.FileSize.Clear();
                    }
                    computer.JobStatus = JobStatus.Waiting;
                    var location = string.Format("\\\\{0}\\{1}\\", computer.Ip, Settings.Default.CollectSourcePath);


                    tasks.Add(
                        Task.Run(
                            () => {
                                try {
                                    EnumerateAndCopyFiles(
                                        computer, location, Settings.Default.CollectDestination,
                                        Settings.Default.CollectPattern, Settings.Default.RecurseIntoDirectories);
                                }
                                catch (Exception e) {
                                    computer.JobStatus = JobStatus.Failed;
                                    computer.Exception = e;
                                }
                            }));
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
            finally
            {
                Dispatcher.Invoke(
                    () => {
                        IsRunning = false;
                        IsCancelling = false;
                    });
            }
        }

        public static string MakeRelativePath(string fromPath, string toPath) {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) {
                return toPath;
            } // path can't be made relative.

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.ToUpperInvariant() == "FILE") {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private void EnumerateAndCopyFiles(
            SourceComputer computer, string location, string destination, string pattern, bool recurseIntoDirectories) {
            if (!location.EndsWith(Path.DirectorySeparatorChar + ""))
                location += Path.DirectorySeparatorChar;
            foreach (
                var file in
                    EnumerateFiles(
                        location,
                        pattern,
                        recurseIntoDirectories
                            ? SearchOption.AllDirectories
                            : SearchOption.TopDirectoryOnly)) {
                var path =
                    Path.Combine(destination, computer.Name, file.Substring(location.Length));
                var directoryInfo = new FileInfo(path).Directory;
                if (directoryInfo != null)
                    Directory.CreateDirectory(directoryInfo.FullName);
                else
                    throw new DirectoryNotFoundException($"Couldn't find directory for path: {path}");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                CopyFile(
                    computer, file, path);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        private void OnCancelled() {
            foreach (var it in Computers.Select((x, i) => new {ComputerEntry = x, Index = i})) {
                if (it.ComputerEntry.JobStatus == JobStatus.Waiting ||
                    it.ComputerEntry.JobStatus == JobStatus.Copying) {
                    it.ComputerEntry.JobStatus = JobStatus.Cancelled;
                }
            }
            Dispatcher.Invoke(() => IsRunning = false);
        }

        private async void CollectButton_Click(object sender, RoutedEventArgs e) {
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
            SourceComputer SourceComputer, string file, string destination) {
            var progressPart = new Progress<FileProgress>(
                p => {
                    lock (SourceComputer.FileSize) {
                        SourceComputer.FileSize[file] = p.Total;
                    }
                    lock (SourceComputer.FileProgress) {
                        SourceComputer.FileProgress[file] = p.Transfered;
                    }
                    SourceComputer.JobStatus = JobStatus.Copying;
                    if (p.Transfered >= p.Total) {
                        SourceComputer.JobStatus = JobStatus.Success;
                    }
                    Dispatcher.InvokeAsync(RecomputeTotals);
                });
            try {
                await FileEx.CopyAsync(
                    file, destination, cancellationTokenSource.Token, progressPart);
            }
            catch (Win32Exception wex) {
                SourceComputer.JobStatus = JobStatus.Failed;
                SourceComputer.Exception = wex;
            }
            catch (OperationCanceledException) {
                if (SourceComputer.JobStatus == JobStatus.Copying
                    || SourceComputer.JobStatus == JobStatus.Waiting) {
                    SourceComputer.JobStatus = JobStatus.Cancelled;
                }
                throw;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            IsCancelling = true;
            cancellationTokenSource.Cancel();
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e) {
            ComputePreviewAsync();
        }

        public static IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOpt) {
            try {
                var dirFiles = Enumerable.Empty<string>();
                if (searchOpt == SearchOption.AllDirectories) {
                    dirFiles = Directory.EnumerateDirectories(path)
                        .SelectMany(x => EnumerateFiles(x, searchPattern, searchOpt));
                }
                return dirFiles.Concat(Directory.EnumerateFiles(path, searchPattern));
            }
            catch (UnauthorizedAccessException ex) {
                return Enumerable.Empty<string>();
            }
        }

        private async Task ComputePreviewAsync() {
            try {
                IsPreviewComputing = true;
                PreviewTreeItems.Clear();
                var tasks = new List<Task>();
                foreach (var computer in Computers) {
                    var location = string.Format("\\\\{0}\\{1}\\", computer.Ip, Settings.Default.CollectSourcePath);

                    var entry = new CollectPreviewComputerEntry();
                    entry.Name = computer.Name;
                    PreviewTreeItems.Add(entry);

                    tasks.Add(
                        Task.Run(
                            () => {
                                entry.Working = true;
                                try {
                                    foreach (
                                        var file in
                                            EnumerateFiles(
                                                location,
                                                Settings.Default.CollectPattern,
                                                Settings.Default.RecurseIntoDirectories
                                                    ? SearchOption.AllDirectories
                                                    : SearchOption.TopDirectoryOnly)) {
                                        entry.Items.Add(new CollectPreviewFileEntry(file));
                                    }
                                }
                                catch (Exception e) {
                                    entry.Exception = e;
                                }
                                finally {
                                    entry.Working = false;
                                }
                            }));
                }
                await Task.WhenAll(tasks);
            }
            catch (Exception e) {
                MessageBox.Show(
                    "Operation failure: an unhandled exception occurred:" + e, "Operation failure",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally {
                IsPreviewComputing = false;
            }
        }

        private void DestinationBrowseButton_OnClick(object sender, RoutedEventArgs e) {
            try {
                IsDestinationBrowseDialogOpen = true;
                var dialog = new FolderBrowserDialog();
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK) {
                    Settings.Default.CollectDestination = dialog.SelectedPath;
                }
            }
            finally {
                IsDestinationBrowseDialogOpen = false;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            if (IsRunning || IsPreviewComputing) {
                e.Cancel = true;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e) {
            ShellExecute(
                IntPtr.Zero, "open", "explorer.exe", Settings.Default.CollectDestination, "", ShowCommands.SW_NORMAL);
        }

        [DllImport("shell32.dll")]
        private static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string lpOperation,
            string lpFile,
            string lpParameters,
            string lpDirectory,
            ShowCommands nShowCmd);

        private void Window_Closed(object sender, EventArgs e)
        {
            Settings.Default.Save();
        }
    }
}