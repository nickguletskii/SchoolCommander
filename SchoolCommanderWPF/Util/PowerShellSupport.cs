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
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using SchoolCommanderWPF.Model;
using SchoolCommanderWPF.Model.Core;

namespace SchoolCommanderWPF.Util
{
    public class PowerShellSupport
    {
        public static readonly PSCredential PsCredential;


        static PowerShellSupport()
        {
            var password = new SecureString();
            var plainPassword
                = Properties.Settings.Default["PowershellPassword"].ToString();
            foreach (var c in plainPassword)
            {
                password.AppendChar(c);
            }
            PsCredential = new PSCredential(Properties.Settings.Default["PowershellUsername"].ToString(), password);
        }

        public static WSManConnectionInfo CreateConnectionInfo(Computer computer, int idleTimeout = 60000, int operationTimeout = 1000, int connectionTimeout = 1000)
        {
            var wSManConnectionInfo = new WSManConnectionInfo(
                false, computer.Name, 5985, "/wsman",
                "http://schemas.microsoft.com/powershell/Microsoft.PowerShell", PowerShellSupport.PsCredential)
            {
                AuthenticationMechanism = AuthenticationMechanism.Default
            };
            wSManConnectionInfo.IdleTimeout = idleTimeout;
            wSManConnectionInfo.OperationTimeout = operationTimeout;
            wSManConnectionInfo.OpenTimeout = connectionTimeout;
            return wSManConnectionInfo;
        }
    }
}
