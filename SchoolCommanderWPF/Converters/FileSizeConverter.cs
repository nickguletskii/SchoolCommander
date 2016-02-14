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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Data;

namespace SchoolCommanderWPF.Converters {
    [ValueConversion(typeof (long), typeof (string))]
    public class FileSizeConverter : IValueConverter {
        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        public static extern long StrFormatByteSize(
            long fileSize
            , [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer
            , int bufferSize);

        public static string StrFormatByteSize(long filesize) {
            var sb = new StringBuilder(11);
            StrFormatByteSize(filesize, sb, sb.Capacity);
            return sb.ToString();
        }

        public object Convert(
            object value, Type targetType, object parameter,
            CultureInfo culture) {
            return StrFormatByteSize((long) value);
        }

        public object ConvertBack(
            object value, Type targetType, object parameter,
            CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}