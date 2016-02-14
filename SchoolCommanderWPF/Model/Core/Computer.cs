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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SchoolCommanderWPF.Util;

namespace SchoolCommanderWPF.Model.Core {
    public class Computer : BindingBase {
        private  readonly SemaphoreSlim _dataUpdateSemaphore = new SemaphoreSlim(1, 1);
        private InternetConnectionStatus _internetConnectionStatus;
        private IPAddress _ip;
        private bool _isOffline;
        private string _mac;
        private string _name;
        private ComputerStatus _status = new ComputerStatus();

        public Computer(IPAddress iPAddress, string name) {
            _internetConnectionStatus = new InternetConnectionStatus(this);

            Ip = iPAddress;
            Name = name;
        }

        public string Mac {
            get { return _mac; }
            set {
                SetProperty(ref _mac, value);
                if (_mac != null) {
                    Directory.CreateDirectory("macs");
                    var filePath = "macs/" + _ip + ".txt";
                    File.WriteAllText(filePath, _mac);
                }
            }
        }

        public bool IsOffline {
            get { return _isOffline; }
            set { SetProperty(ref _isOffline, value); }
        }

        public IPAddress Ip {
            get { return _ip; }
            set { SetProperty(ref _ip, value); }
        }

        public ComputerStatus Status {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }

        public string Name {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public InternetConnectionStatus InternetConnectionStatus {
            get { return _internetConnectionStatus; }
            set {
                SetProperty(ref _internetConnectionStatus, value);
                _internetConnectionStatus.PropertyChanged +=
                    (sender, args) => OnPropertyChangedExpl("InternetConnectionStatus");
            }
        }


        public byte[] GetMacAsBytes() {
            var bytes = Mac.Split('-');
            var parsed = new byte[bytes.Length];

            for (var x = 0; x < bytes.Length; x++) {
                parsed[x] = byte.Parse(bytes[x], NumberStyles.HexNumber);
            }
            return parsed;
        }


        public async Task WakeOnLanAsync() {
            await Task.Run(
                () => {
                    var packet = new List<byte>();
                    for (var i = 0; i < 6; i++)
                        packet.Add(0xFF);
                    for (var i = 0; i < 16; i++)
                        packet.AddRange(GetMacAsBytes());
                    var client = new UdpClient();
                    client.Connect(IPAddress.Broadcast, 7);
                    client.Send(packet.ToArray(), packet.Count);
                });
        }

        private string GetMacFromCache() {
            var filePath = "macs/" + _ip;
            if (File.Exists(filePath)) {
                return File.ReadAllText(filePath);
            }
            return null;
        }


        public async Task UpdateDataAsync(TimeSpan period) {
            if (period != TimeSpan.Zero) {
                var allowed = await _dataUpdateSemaphore.WaitAsync(period);
                if (!allowed) {
                    return;
                }
            }
            else {
                await _dataUpdateSemaphore.WaitAsync();
            }
            try {
                await Task.Run(
                    () => {
                        if (!Debugger.IsAttached) {
                            var pingSender = new Ping();
                            var reply = pingSender.Send(Ip, 100);
                            if (reply.Status != IPStatus.Success) {
                                Status.Ping = "Ping failed: " + reply.Status;
                                IsOffline = true;
                                return;
                            }
                            IsOffline = false;
                            Status.Ping = "Online, response time: " + reply.RoundtripTime;
                        }
                        else {
                            IsOffline = false;
                            Status.Ping = "Can't ping when debugging.";
                        }
                        UpdateMac();
                        UpdateName();
                    });
            }
            catch (Exception e) {
                Status.DataUpdateException = e;
            }
            finally {
                _dataUpdateSemaphore.Release();
            }
        }

        private void UpdateMac() {
            Mac = GetMacFromCache() ?? MACResolver.GetMACAddressFromARP(Ip);
        }

        private void UpdateName() {
            var regex = new Regex(@"^([^\.]+)(\..*)?$");
            var fullName = Dns.GetHostEntry(Ip.ToString()).HostName;
            var match = regex.Match(fullName);
            Name = match.Groups[1].Value;
        }

        protected bool Equals(Computer other) {
            return Equals(_ip, other._ip) && string.Equals(_name, other._name);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Computer) obj);
        }

        public override int GetHashCode() {
            unchecked {
                return ((_ip != null ? _ip.GetHashCode() : 0)*397) ^ (_name != null ? _name.GetHashCode() : 0);
            }
        }
    }
}