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
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Windows;
using System.Windows.Data;
using SchoolCommanderWPF.Model.Core;

namespace SchoolCommanderWPF.Model.PowerShellTask {
    public class RemoteComputer : Computer {
        private PowerShellJobStatus _jobStatus;

        private readonly ConcurrentDictionary<int, int> _progressEntries = new ConcurrentDictionary<int, int>();
        private readonly object lockObj = new object();

        public RemoteComputer() : base(null, null) {
            BindingOperations.EnableCollectionSynchronization(LogEntries, lockObj);
        }

        public RemoteComputer(Computer computer) : base(computer.Ip, computer.Name) {
            BindingOperations.EnableCollectionSynchronization(LogEntries, lockObj);
        }

        public PowerShellJobStatus JobStatus {
            get { return _jobStatus; }
            set { SetProperty(ref _jobStatus, value); }
        }


        public int ErrorCount { get; set; } = 0;
        public int WarningCount { get; set; } = 0;
        public ObservableCollection<PowerShellEntry> LogEntries { get; } = new ObservableCollection<PowerShellEntry>();

        public void AddLogEntry(LogEntry entry) {
            lock (LogEntries) {
                LogEntries.Add(entry);
            }
        }

        public void LogProgress(RemoteComputer computer, ProgressRecord progressRecord) {
            lock (LogEntries) {
                var id = _progressEntries.GetOrAdd(
                    progressRecord.ActivityId, i => {
                        var progressEntry =
                            new ProgressEntry(computer.Name);
                        progressEntry.Activity = progressRecord.Activity;
                        progressEntry.ActivityId = progressRecord.ActivityId;
                        progressEntry.Status = progressRecord.StatusDescription;
                        progressEntry.CurrentOperation = progressRecord.CurrentOperation;
                        if (progressRecord.PercentComplete < 0) {
                            progressEntry.Visibility = Visibility.Hidden;
                        }
                        else {
                            progressEntry.Visibility = Visibility.Visible;
                        }
                        LogEntries.Add(progressEntry);
                        return LogEntries.Count - 1;
                    });
                ((ProgressEntry) LogEntries[id]).PercentComplete = progressRecord.PercentComplete;
            }
        }
    }
}