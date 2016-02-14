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
using System.Windows;

namespace SchoolCommanderWPF.Model.PowerShellTask {
    public class ProgressEntry : PowerShellEntry {
        private string _activity;
        private int _activityId;
        private string _currentOperation;
        private int _percentComplete;
        private string _status;
        private Visibility _visibility;

        public ProgressEntry(string computerName)
            : base(computerName) {
        }

        public string Activity {
            get { return _activity; }
            set { SetProperty(ref _activity, value); }
        }

        public int ActivityId {
            get { return _activityId; }
            set { SetProperty(ref _activityId, value); }
        }

        public string Status {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }

        public int PercentComplete {
            get { return _percentComplete; }
            set { SetProperty(ref _percentComplete, value); }
        }

        public string CurrentOperation {
            get { return _currentOperation; }
            set { SetProperty(ref _currentOperation, value); }
        }

        public Visibility Visibility {
            get { return _visibility; }
            set { SetProperty(ref _visibility, value); }
        }
    }
}