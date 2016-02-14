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
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

namespace SchoolCommanderWPF.Util {
    public class MACResolver {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(
            int DestIP, int SrcIP, byte[] pMacAddr,
            ref uint PhyAddrLen);

        public static string GetMACAddressFromARP(IPAddress ip) {
            var macAddr = new byte[6];
            var macAddrLen = (uint) macAddr.Length;

            int error;
            if ((error = SendARP(BitConverter.ToInt32(ip.GetAddressBytes(), 0), 0, macAddr, ref macAddrLen)) != 0) {
                throw new Win32Exception(error);
            }

            var str = new string[(int) macAddrLen];
            for (var i = 0; i < macAddrLen; i++)
                str[i] = macAddr[i].ToString("x2");
            return string.Join(":", str);
        }
    }
}