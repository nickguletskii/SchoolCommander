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
using System.Collections.ObjectModel;
using System.Windows.Data;
using BindingBase = SchoolCommanderWPF.Util.BindingBase;

namespace SchoolCommanderWPF.Model.CollectTask {
    public class CollectPreviewComputerEntry : BindingBase {
        private readonly object _itemsLock = new object();
        private Exception _exception;
        private string _name;
        private bool _working;

        public CollectPreviewComputerEntry() {
            Items = new ObservableCollection<CollectPreviewFileEntry>();
            BindingOperations.EnableCollectionSynchronization(Items, _itemsLock);
        }

        public string Name {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }


        public bool Working {
            get { return _working; }
            set { SetProperty(ref _working, value); }
        }

        public ObservableCollection<CollectPreviewFileEntry> Items { get; }

        public Exception Exception {
            get { return _exception; }
            set { SetProperty(ref _exception, value); }
        }
    }
}