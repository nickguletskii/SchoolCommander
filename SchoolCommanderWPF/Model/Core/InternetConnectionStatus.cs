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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using SchoolCommanderWPF.Properties;
using SchoolCommanderWPF.Util;

namespace SchoolCommanderWPF.Model.Core {
    public class InternetConnectionStatus : BindingBase {
        private const string CheckInet =
            "Get-NetRoute -ErrorAction Stop | Where DestinationPrefix -EQ \"0.0.0.0/0\" | measure";

        private const string EnableInet =
            "New-NetRoute -DestinationPrefix \"0.0.0.0/0\" -InterfaceIndex ((Get-NetIPAddress | where IPAddress -Like \"{0}\").InterfaceIndex) -NextHop {1} -RouteMetric 0";

        private const string DisableInet =
            "Remove-NetRoute -DestinationPrefix \"0.0.0.0/0\" -Confirm:$false";

        private static readonly SemaphoreSlim dataUpdateSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim runspaceSemaphore = new SemaphoreSlim(3, 3);

        private readonly Computer _computer;

        private readonly BlockingCollection<Runspace> runspacePool = new BlockingCollection<Runspace>();
        private Exception _accessLevelSynchronizationException;
        private bool _changingInternetStatus;
        private InternetAccessLevel _effectiveAccessLevel = InternetAccessLevel.NotSet;
        private Exception _effectiveAccessLevelCheckException;

        public InternetConnectionStatus(Computer computer) {
            _computer = computer;
        }

        public bool ChangingInternetStatus {
            get { return _changingInternetStatus; }
            set { SetProperty(ref _changingInternetStatus, value); }
        }

        public InternetAccessLevel EffectiveAccessLevel {
            get { return _effectiveAccessLevel; }
            set {
                SetProperty(ref _effectiveAccessLevel, value);
                OnPropertyChangedExpl("EffectiveAccessLevelDetails");
            }
        }

        public Exception AccessLevelSynchronizationException {
            get { return _accessLevelSynchronizationException; }
            set { SetProperty(ref _accessLevelSynchronizationException, value); }
        }

        public Exception EffectiveAccessLevelCheckException {
            get { return _effectiveAccessLevelCheckException; }
            set {
                SetProperty(ref _effectiveAccessLevelCheckException, value);
                OnPropertyChangedExpl("EffectiveAccessLevelDetails");
            }
        }

        public string EffectiveAccessLevelDetails {
            get {
                if (EffectiveAccessLevel != InternetAccessLevel.NotSet) {
                    return EffectiveAccessLevel == InternetAccessLevel.Allowed
                        ? "This computer HAS internet access."
                        : "This computer DOES NOT have internet access.";
                }
                if (EffectiveAccessLevelCheckException == null) {
                    return "It is still unknown whether this computer has access or not. Please wait.";
                }
                return "Couldn't check internet access.\n" + EffectiveAccessLevelCheckException;
            }
        }

        public async Task<Runspace> CreateRunspaceAsync() {
            return await Task.Run(
                () => {
                    var wSManConnectionInfo = PowerShellSupport.CreateConnectionInfo(_computer);
                    Runspace runspace = null;
                    try {
                        runspace = RunspaceFactory.CreateRunspace(wSManConnectionInfo);
                        runspace.Open();

                        return runspace;
                    }
                    catch {
                        runspace?.Dispose();
                        throw;
                    }
                });
        }


        public async Task<bool> GetEffectiveAccessLevelCheckPowerShellAsync(bool recursionGuard = false) {
            await runspaceSemaphore.WaitAsync();
            Runspace runspace = null;
            try {
                runspace = await GetRunspaceAsync();
                using (var pipeline = runspace.CreatePipeline(CheckInet)) {
                    var output = pipeline.Invoke();
                    return (int) output[0].Properties["Count"].Value == 1 ? true : false;
                }
            }
            finally {
                runspaceSemaphore.Release(1);
                if (runspace?.RunspaceStateInfo?.State == RunspaceState.Opened) {
                    RecommitToRunspacePool(runspace);
                }
                else {
                    runspace?.Dispose();
                }
            }
        }


        public async Task SynchronizeAsync(bool internetEnabled) {
            ChangingInternetStatus = true;

            Runspace runspace = null;
            try
            {
                await runspaceSemaphore.WaitAsync();
                runspace = await GetRunspaceAsync();
                var syncScript = GetSynchronizationScript(internetEnabled);
                if (syncScript == null) {
                    return;
                }
                
                using (var pipeline = runspace.CreatePipeline(syncScript)) {
                    pipeline.Invoke();
                }
            }
            catch (Exception e)
            {
                AccessLevelSynchronizationException = e;
            }
            finally {
                runspaceSemaphore.Release(1);
                if (runspace?.RunspaceStateInfo?.State == RunspaceState.Opened) {
                    RecommitToRunspacePool(runspace);
                }
                else {
                    runspace?.Dispose();
                }
                ChangingInternetStatus = false;
                UpdateStatusAsync(TimeSpan.Zero);
            }
        }

        private Task<Runspace> GetRunspaceAsync() {
            Runspace runspace;
            while (runspacePool.TryTake(out runspace)) {
                if (runspace.RunspaceStateInfo.State == RunspaceState.Opened) {
                    return Task.FromResult(runspace);
                }
                runspace?.Dispose();
            }
            return CreateRunspaceAsync();
        }

        private void RecommitToRunspacePool(Runspace runspace) {
            if (runspacePool.Count > 3) {
                runspace.Dispose();
                return;
            }
            runspacePool.Add(runspace);
        }

        private string GetSynchronizationScript(bool internetEnabled) {
            if (internetEnabled) {
                return string.Format(
                    EnableInet, Settings.Default["NetworkInterfacePattern"],
                    Settings.Default["DefaultGateway"]);
            }
            return DisableInet;
        }

        public async Task UpdateStatusAsync(TimeSpan period) {
            if (period != TimeSpan.Zero) {
                var allowed = await dataUpdateSemaphore.WaitAsync(period);
                if (!allowed) {
                    return;
                }
            }
            else {
                await dataUpdateSemaphore.WaitAsync();
            }

            try {
                EffectiveAccessLevel = await GetEffectiveAccessLevelCheckPowerShellAsync()
                    ? InternetAccessLevel.Allowed
                    : InternetAccessLevel.Forbidden;
                EffectiveAccessLevelCheckException = null;
            }
            catch (Exception e) {
                EffectiveAccessLevel = InternetAccessLevel.NotSet;
                EffectiveAccessLevelCheckException = e;
            }
            finally {
                dataUpdateSemaphore.Release();
            }
        }
    }
}