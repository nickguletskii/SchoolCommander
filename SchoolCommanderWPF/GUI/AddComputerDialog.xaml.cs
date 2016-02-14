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
    /// Interaction logic for AddComputerDialog.xaml
    /// </summary>
    public partial class AddComputerDialog : Window {

        public static readonly DependencyProperty ComputerNameProperty = DependencyProperty.Register(
            "ComputerName", typeof (string), typeof (AddComputerDialog), new PropertyMetadata(""));

        public string ComputerName {
            get { return (string) GetValue(ComputerNameProperty); }
            set { SetValue(ComputerNameProperty, value); }
        }

        public static readonly DependencyProperty ComputerIpProperty = DependencyProperty.Register(
            "ComputerIp", typeof (string), typeof (AddComputerDialog), new PropertyMetadata(""));

        public string ComputerIp {
            get { return (string) GetValue(ComputerIpProperty); }
            set { SetValue(ComputerIpProperty, value); }
        }
        public AddComputerDialog()
        {
            InitializeComponent();
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
         
        }
    }
}