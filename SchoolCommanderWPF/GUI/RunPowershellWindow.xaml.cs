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
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using SchoolCommanderWPF.Model.Core;
using SchoolCommanderWPF.Model.PowerShellTask;
using SchoolCommanderWPF.Properties;
using SchoolCommanderWPF.Repository;
using SchoolCommanderWPF.Util;

namespace SchoolCommanderWPF.GUI {
    /// <summary>
    ///     Interaction logic for RunPowershellWindow.xaml
    /// </summary>
    public partial class RunPowershellWindow : Window {
        public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
            "IsRunning", typeof (bool), typeof (RunPowershellWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty CopyTaskExistsProperty = DependencyProperty.Register(
            "CopyTaskExists", typeof (bool), typeof (RunPowershellWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty IsCancellingProperty = DependencyProperty.Register(
            "IsCancelling", typeof (bool), typeof (RunPowershellWindow), new PropertyMetadata(default(bool)));

        private readonly object computersLock = new object();

        private CancellationTokenSource cancellationTokenSource;

        public RunPowershellWindow() {
            throw new NotSupportedException();
        }

        public RunPowershellWindow(IList<Computer> selectedItems) {
            Computers = new RemoteComputerManager(selectedItems.Select(c => new RemoteComputer(c)));
            BindingOperations.EnableCollectionSynchronization(Computers, computersLock);
            InitializeComponent();

            ProgressTable.ItemsSource = Computers;
            FileNamesList.ItemsSource = FileNames;
        }

        public RunPowershellWindow(IList<Computer> selectedItems, string item) : this(selectedItems) {
            Computers = new RemoteComputerManager(selectedItems.Select(c => new RemoteComputer(c)));
            BindingOperations.EnableCollectionSynchronization(Computers, computersLock);
            InitializeComponent();

            FileNames.Add(item);
            ProgressTable.ItemsSource = Computers;
            FileNamesList.ItemsSource = FileNames;
        }

        public RemoteComputerManager Computers { get; set; }
        public ObservableCollection<string> FileNames { get; set; } = new ObservableCollection<string>();

        public bool IsRunning {
            get { return (bool) GetValue(IsRunningProperty); }
            set { SetValue(IsRunningProperty, value); }
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

        private async Task RunScriptsAsync() {
            if (IsRunning)
                return;
            try {
                cancellationTokenSource = new CancellationTokenSource();

                Dispatcher.Invoke(
                    () => {
                        IsRunning = true;
                        CopyTaskExists = true;
                    });
                IsRunning = true;
                CopyTaskExists = true;
                var tasks = new List<Task>();
                foreach (var computer in Computers) {
                    tasks.Add(RunPowershell(computer));
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

        private async Task RunPowershell(RemoteComputer powershellComputer) {
            var connectionInfo = PowerShellSupport.CreateConnectionInfo(powershellComputer);

            await Task.Run(
                () => {
                    try {
                        powershellComputer.JobStatus = PowerShellJobStatus.Connecting;

                        cancellationTokenSource.Token.ThrowIfCancellationRequested();


                        using (var runspace = RunspaceFactory.CreateRunspace(connectionInfo)) {
                            cancellationTokenSource.Token.ThrowIfCancellationRequested();
                            using (cancellationTokenSource.Token.Register(
                                () => runspace.Close())) {
                                runspace.Open();
                            }
                            cancellationTokenSource.Token.ThrowIfCancellationRequested();

                            using (var powershell = PowerShell.Create()) {
                                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                                powershell.Runspace = runspace;
                                foreach (var script in FileNames) {
                                    powershell.AddScript(File.ReadAllText(script));
                                }
                                var input = new PSDataCollection<PSObject>();
                                var output = new PSDataCollection<PSObject>();
                                powershell.Streams.Error.DataAdded +=
                                    (s, ev) => ErrorOnDataAdded(s, ev, powershellComputer);
                                powershell.Streams.Debug.DataAdded +=
                                    (s, ev) => DebugOnDataAdded(s, ev, powershellComputer);
                                powershell.Streams.Progress.DataAdded +=
                                    (s, ev) => ProgressOnDataAdded(s, ev, powershellComputer);
                                powershell.Streams.Verbose.DataAdded +=
                                    (s, ev) => VerboseOnDataAdded(s, ev, powershellComputer);
                                powershell.Streams.Warning.DataAdded +=
                                    (s, ev) => WarningOnDataAdded(s, ev, powershellComputer);

                                powershellComputer.JobStatus = PowerShellJobStatus.Invoking;


                                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                                using (cancellationTokenSource.Token.Register(
                                    () => {
                                        powershell.InvocationStateChanged += (sender, args) => {
                                            switch (args.InvocationStateInfo.State) {
                                                case PSInvocationState.Failed:
                                                    powershellComputer.JobStatus = PowerShellJobStatus.Failed;
                                                    break;
                                                case PSInvocationState.Completed:
                                                    powershellComputer.JobStatus = PowerShellJobStatus.Completed;
                                                    break;
                                                case PSInvocationState.Stopping:
                                                    powershellComputer.JobStatus = PowerShellJobStatus.Cancelling;

                                                    break;
                                                case PSInvocationState.Stopped:
                                                    powershellComputer.JobStatus = PowerShellJobStatus.Cancelled;

                                                    break;
                                            }
                                        };
                                        powershell.Stop();
                                    })) {
                                    powershell.Invoke(input, output);
                                }
                                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                                powershellComputer.JobStatus = PowerShellJobStatus.Completed;
                            }
                        }
                    }
                    catch (RemoteException e) {
                        if (cancellationTokenSource.IsCancellationRequested) {
                            powershellComputer.JobStatus = PowerShellJobStatus.Cancelled;
                            return;
                        }
                        throw;
                    }
                    catch (PSRemotingDataStructureException ex) {
                        if (ex?.ErrorRecord?.Exception?.Message == "The pipeline has been stopped.") {
                            powershellComputer.JobStatus = PowerShellJobStatus.Cancelled;
                        }
                    }
                    catch (PSRemotingTransportException ex) {
                        if (ex.ErrorRecord != null) {
                            WriteErrorRecord(powershellComputer, ex.ErrorRecord);
                        }
                        powershellComputer.JobStatus = PowerShellJobStatus.CouldntConnect;
                    }
                    catch (OperationCanceledException ex) {
                        switch (powershellComputer.JobStatus) {
                            case PowerShellJobStatus.Waiting:
                            case PowerShellJobStatus.Invoking:
                            case PowerShellJobStatus.Connecting:
                            case PowerShellJobStatus.Cancelling:
                                powershellComputer.JobStatus = PowerShellJobStatus.Cancelled;
                                break;
                        }
                    }
                    catch (Exception ex) {
                        powershellComputer.JobStatus = PowerShellJobStatus.Failed;

                        MessageBox.Show(
                            "An internal error has occurred!\n" + ex, "Internal error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
        }

        private void DebugOnDataAdded(
            object sender, DataAddedEventArgs dataAddedEventArgs, RemoteComputer powershellComputer) {
            var record = ((PSDataCollection<WarningRecord>) sender)[dataAddedEventArgs.Index];
            WriteDebugRecord(powershellComputer, record);
        }

        private void WriteDebugRecord(RemoteComputer powershellComputer, WarningRecord record) {
            powershellComputer.AddLogEntry(new LogEntry(powershellComputer.Name, record.Message, LogEntryType.Debug));
        }

        private void ErrorOnDataAdded(
            object sender, DataAddedEventArgs dataAddedEventArgs, RemoteComputer powershellComputer) {
            var record = ((PSDataCollection<ErrorRecord>) sender)[dataAddedEventArgs.Index];
            powershellComputer.ErrorCount++;
            WriteErrorRecord(powershellComputer, record);
        }

        private void WarningOnDataAdded(
            object sender, DataAddedEventArgs dataAddedEventArgs, RemoteComputer powershellComputer) {
            var record = ((PSDataCollection<WarningRecord>) sender)[dataAddedEventArgs.Index];

            powershellComputer.WarningCount++;
            WriteWarningRecord(powershellComputer, record);
        }

        private void WriteWarningRecord(RemoteComputer powershellComputer, WarningRecord record) {
            powershellComputer.AddLogEntry(new LogEntry(powershellComputer.Name, record.Message, LogEntryType.Warning));
        }

        private void VerboseOnDataAdded(
            object sender, DataAddedEventArgs dataAddedEventArgs, RemoteComputer powershellComputer) {
            var record = ((PSDataCollection<VerboseRecord>) sender)[dataAddedEventArgs.Index];
            WriteVerboseRecord(powershellComputer, record);
        }

        private void WriteVerboseRecord(RemoteComputer powershellComputer, VerboseRecord record) {
            powershellComputer.AddLogEntry(new LogEntry(powershellComputer.Name, record.Message, LogEntryType.Verbose));
        }

        private void ProgressOnDataAdded(
            object sender, DataAddedEventArgs dataAddedEventArgs, RemoteComputer powershellComputer) {
            var progressRecord = ((PSDataCollection<ProgressRecord>) sender)[dataAddedEventArgs.Index];
            powershellComputer.LogProgress(powershellComputer, progressRecord);
        }

        private void WriteErrorRecord(RemoteComputer powershellComputer, ErrorRecord record) {
            powershellComputer.AddLogEntry(new LogEntry(powershellComputer.Name, record.ToString(), LogEntryType.Error));
        }


        private void OnCancelled() {
            foreach (var it in Computers) {
                switch (it.JobStatus) {
                    case PowerShellJobStatus.Waiting:
                    case PowerShellJobStatus.Connecting:
                    case PowerShellJobStatus.Invoking:
                    case PowerShellJobStatus.Cancelling:
                        it.JobStatus = PowerShellJobStatus.Cancelled;
                        break;
                }
            }
            IsRunning = false;
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e) {
            await RunScriptsAsync();
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