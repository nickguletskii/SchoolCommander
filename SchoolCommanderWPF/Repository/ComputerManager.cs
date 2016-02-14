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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SchoolCommanderWPF.Model.Core;
using SchoolCommanderWPF.Properties;

namespace SchoolCommanderWPF.Repository {
    public class ComputerManager : ObservableCollection<Computer> {
        private readonly ComputersConfigListSettings settings = new ComputersConfigListSettings();

        public async Task LoadComputers() {
            await Task.Run(
                () => {
                    foreach (var c in from computer in settings.Computers
                        select new Computer(IPAddress.Parse(computer.Ip), computer.Name)) {
                        AddComputer(c);
                    }
                });
        }

        public void AddComputer(Computer c) {
            Add(c);
            Task.Run(
                async () => {
                    await c.UpdateDataAsync(TimeSpan.Zero);
                    await c.InternetConnectionStatus.UpdateStatusAsync(TimeSpan.Zero);
                });
        }

        public void SaveComputers() {
            var list = new List<ComputerSerialization>();
            foreach (var c in this) {
                var computerSerialization = new ComputerSerialization();
                computerSerialization.Ip = c.Ip.ToString();
                computerSerialization.Name = c.Name;
                list.Add(computerSerialization);
            }

            settings.Computers = list;
            settings.Save();
        }
    }
}