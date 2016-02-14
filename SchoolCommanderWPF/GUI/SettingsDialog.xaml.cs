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

namespace SchoolCommanderWPF.GUI {
    /// <summary>
    ///     Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window {
        public static readonly DependencyProperty PowerShellUsernameProperty =
            DependencyProperty.Register(
                "PowerShellUsername", typeof (string), typeof (SettingsDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty DefaultGatewayProperty = DependencyProperty.Register(
            "DefaultGateway", typeof (string), typeof (SettingsDialog), new PropertyMetadata(null));

        public static readonly DependencyProperty NetworkInterfacePatternProperty =
            DependencyProperty.Register(
                "NetworkInterfacePattern", typeof (string), typeof (SettingsDialog), new PropertyMetadata(null));

        public SettingsDialog() {
            InitializeComponent();
        }

        public string PowerShellUsername {
            get { return (string) GetValue(PowerShellUsernameProperty); }
            set { SetValue(PowerShellUsernameProperty, value); }
        }

        public string PowerShellPassword {
            get { return PowerShellPasswordBox.Password; }
            set { PowerShellPasswordBox.Password = value; }
        }

        public string DefaultGateway {
            get { return (string) GetValue(DefaultGatewayProperty); }
            set { SetValue(DefaultGatewayProperty, value); }
        }

        public string NetworkInterfacePattern {
            get { return (string) GetValue(NetworkInterfacePatternProperty); }
            set { SetValue(NetworkInterfacePatternProperty, value); }
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            DialogResult = true;
        }
    }
}