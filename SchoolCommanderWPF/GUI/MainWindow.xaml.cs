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
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SchoolCommanderWPF.Model;
using SchoolCommanderWPF.Model.Core;
using SchoolCommanderWPF.Properties;
using SchoolCommanderWPF.Repository;

namespace SchoolCommanderWPF.GUI {
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private readonly object _computersLock = new object();

        public MainWindow() {
            InitializeComponent();

            BindingOperations.EnableCollectionSynchronization(Computers, _computersLock);

            ComputersTable.ItemsSource = Computers;
            Computers.LoadComputers();

            var dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += (sender, args) => {
                foreach (var computer in Computers) {
                    computer.UpdateDataAsync(TimeSpan.FromSeconds(10));
                    computer.InternetConnectionStatus.UpdateStatusAsync(TimeSpan.FromSeconds(10));
                }
            };
            dispatcherTimer.Interval = TimeSpan.FromSeconds(10);
            dispatcherTimer.Start();
            string path = Path.GetDirectoryName(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
            string scriptsPath = Path.Combine(path, "scripts" + Path.DirectorySeparatorChar);
            Directory.CreateDirectory(scriptsPath);

            ActionsList.ItemsSource = from file in Directory.GetFiles(scriptsPath) select GetActionEntry(file);
        }

        private ActionEntry GetActionEntry(string file) {
            string line;
            string name;
            using (StreamReader reader = new StreamReader(file))
            {
                line = reader.ReadLine();
            }
            Regex regex = new Regex(@"#\s*(?<name>.*)");
            Match match = regex.Match(line);

            if (match.Success) {
                name = match.Groups["name"].Value;
            }
            else {
                name = line;
            }

            var entry = new ActionEntry();
            entry.Path = file;
            entry.Name = name;
            return entry;
        }

        private ComputerManager Computers { get; } = new ComputerManager();
        private int openWindowCount = 0;
        private void Send_Click(object sender, RoutedEventArgs e) {
            var sendFilesWindow = new SendFilesWindow(
                ComputersTable.SelectedItems.Cast<Computer>().ToList()
                );
            sendFilesWindow.Show();
            openWindowCount++;
            sendFilesWindow.Closed += (o, args) => openWindowCount--;
        }

        private void Collect_Click(object sender, RoutedEventArgs e) {
            var collectFilesWindow = new CollectFilesWindow(
                ComputersTable.SelectedItems.Cast<Computer>().ToList()
                );
            collectFilesWindow.Show();
            openWindowCount++;
            collectFilesWindow.Closed += (o, args) => openWindowCount--;
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            var runPowershellWindow = new RunPowershellWindow(
                ComputersTable.SelectedItems.Cast<Computer>().ToList()
                );
            runPowershellWindow.Show();
            openWindowCount++;
            runPowershellWindow.Closed += (o, args) => openWindowCount--;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (openWindowCount > 0) {
                var result = MessageBox.Show(
                    "There are open School Commander windows. Would you like to force shutdown?", "Force close?",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes) {
                    Environment.Exit(0);
                }
                else {
                    e.Cancel = true;
                }
            }
            else {
                Application.Current.Shutdown();
            }
        }

        private void RemoveComputers_Click(object sender, RoutedEventArgs e) {
            var result = MessageBox.Show(
                "Are you sure you want to remove the following computers?", "Remove Computers", MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if(result != MessageBoxResult.Yes)
                return;
            List<Computer> comps = ComputersTable.SelectedItems.Cast<Computer>().ToList();
            foreach(var c in comps)
            {
                Computers.Remove(c);
            }
        }

        private void AddComputer_Click(object sender, RoutedEventArgs e) {
            var dialog = new AddComputerDialog();
            dialog.Owner = this;
            bool? b = dialog.ShowDialog();
            if (b == true) {
                Computers.AddComputer(new Computer(IPAddress.Parse(dialog.ComputerIp), dialog.ComputerName));
            }
        }

        private void SaveComputers_Click(object sender, RoutedEventArgs e) {
            Computers.SaveComputers();
        }
        
        private void InetOffButton_OnClick(object sender, RoutedEventArgs e) {
            var computer = ((FrameworkElement)sender).DataContext as Computer;

             computer.InternetConnectionStatus.SynchronizeAsync(false);
        }

        private void InetOnButton_OnClick(object sender, RoutedEventArgs e) {
            var computer = ((FrameworkElement)sender).DataContext as Computer;
            computer.InternetConnectionStatus.SynchronizeAsync(true);
        }

        private void Settings_Click(object sender, RoutedEventArgs e) {
            var settingsDialog = new SettingsDialog();
            settingsDialog.Owner = this;
            settingsDialog.PowerShellUsername = Settings.Default.PowerShellUsername;
            settingsDialog.PowerShellPassword = Settings.Default.PowerShellPassword;
            settingsDialog.DefaultGateway = Settings.Default.DefaultGateway;
            settingsDialog.NetworkInterfacePattern= Settings.Default.NetworkInterfacePattern;
            if (settingsDialog.ShowDialog() == true)
            {
                Settings.Default.PowerShellUsername = settingsDialog.PowerShellUsername;
                Settings.Default.PowerShellPassword = settingsDialog.PowerShellPassword;
                Settings.Default.DefaultGateway = settingsDialog.DefaultGateway;
                Settings.Default.NetworkInterfacePattern = settingsDialog.NetworkInterfacePattern;
                Settings.Default.Save();
            }
        }

        private void ActionItem_Clicked(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount < 2)
                return;
            var item = (sender as ListBoxItem)?.DataContext as ActionEntry;
            if (item?.Path != null)
            {
                var runPowershellWindow = new RunPowershellWindow(
                    ComputersTable.SelectedItems.Cast<Computer>().ToList(),
                    item.Path
                    );
                runPowershellWindow.Show();
                openWindowCount++;
                runPowershellWindow.Closed += (o, args) => openWindowCount--;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) {
            foreach (var c in Computers) {
                c.InternetConnectionStatus.EffectiveAccessLevel = InternetAccessLevel.NotSet;
                c.InternetConnectionStatus.EffectiveAccessLevelCheckException = null;
                c.InternetConnectionStatus.UpdateStatusAsync(TimeSpan.Zero);
            }
        }
    }
}