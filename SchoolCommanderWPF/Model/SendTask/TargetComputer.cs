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
using System.ComponentModel;
using System.Linq;
using SchoolCommanderWPF.Model.Core;
using SchoolCommanderWPF.Util;

namespace SchoolCommanderWPF.Model.SendTask {
    public class TargetComputer : Computer {
        private Win32Exception _exception;
        private JobStatus _jobStatus;

        public TargetComputer() : base(null, null) {
        }

        public TargetComputer(Computer computer) : base(computer.Ip, computer.Name) {
            FileSize.CollectionChanged += (sender, args) => {
                OnPropertyChangedExpl("FileSize");
                OnPropertyChangedExpl("TotalSize");
                OnPropertyChangedExpl("Progress");
            };
            FileProgress.CollectionChanged += (sender, args) => {
                OnPropertyChangedExpl("FileProgress");
                OnPropertyChangedExpl("BytesCopied");
                OnPropertyChangedExpl("Progress");
            };
        }

        public JobStatus JobStatus {
            get { return _jobStatus; }
            set { SetProperty(ref _jobStatus, value); }
        }

        public ObservableDictionary<string, long> FileSize { get; } = new ObservableDictionary<string, long>();

        public long TotalSize {
            get {
                lock (FileSize) {
                    return FileSize.Values.Aggregate(0l, (x, y) => x + y);
                }
            }
        }

        public long BytesCopied {
            get {
                lock (FileProgress) {
                    return FileProgress.Values.Aggregate(0l, (x, y) => x + y);
                }
            }
        }

        public float Progress {
            get {
                if (TotalSize == 0)
                    return 0.0f;
                return (float) BytesCopied/TotalSize;
            }
        }

        public ObservableDictionary<string, long> FileProgress { get; } = new ObservableDictionary<string, long>();

        public Win32Exception Exception {
            get { return _exception; }
            set { SetProperty(ref _exception, value); }
        }

    }
}