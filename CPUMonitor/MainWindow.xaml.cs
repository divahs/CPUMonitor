using LiveCharts;
using LiveCharts.Wpf;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Recognition;
using System.Windows;
using System.Windows.Threading;

namespace CPUMonitor
{
    public partial class MainWindow : Window
    {
        // Hardware monitoring
        private Computer computer;

        // Timers
        private DispatcherTimer timer;

        // Chart data
        private ChartValues<double> cpuTemps;

        // Thresholds
        private double warningThreshold = 70;
        private double criticalThreshold = 85;

        // Voice recognition
        private SpeechRecognitionEngine recognizer;
        private bool isVoiceCommandActive = true;

        // Fan Speed Simulation
        private int fanSpeedSetting = 50; // Starting at 50%

        public MainWindow()
        {
            InitializeComponent();

            // Initialize components
            SetupChart();
            InitializeHardwareMonitor();
            InitializeVoiceCommands();
            InitializeTimers();
        }

        #region Initialization Methods

        private void SetupChart()
        {
            cpuTemps = new ChartValues<double>();

            cpuChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "CPU Temp (°C)",
                    Values = cpuTemps
                }
            };

            cpuChart.AxisX.Add(new Axis
            {
                Title = "Time",
                Labels = new List<string>()
            });

            cpuChart.AxisY.Add(new Axis
            {
                Title = "Temperature (°C)",
                MinValue = 0,
                MaxValue = 100
            });
        }

        private void InitializeHardwareMonitor()
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsGpuEnabled = true,
                // IsFanControllerEnabled = true // Removed due to error
            };
            computer.Open();
        }

        private void InitializeVoiceCommands()
        {
            recognizer = new SpeechRecognitionEngine();

            var commands = new Choices();
            commands.Add(new string[] { "increase fan speed", "lower fan speed" });

            var grammarBuilder = new GrammarBuilder();
            grammarBuilder.Append(commands);

            var grammar = new Grammar(grammarBuilder);

            recognizer.LoadGrammar(grammar);
            recognizer.SetInputToDefaultAudioDevice();
            recognizer.SpeechRecognized += Recognizer_SpeechRecognized;

            recognizer.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void InitializeTimers()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += UpdateData;
            timer.Start();
        }

        #endregion

        #region Event Handlers

        private void UpdateData(object sender, EventArgs e)
        {
            UpdateTemperatureChart();
            UpdateProcesses();
        }

        private void SetThresholds_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(warningThresholdBox.Text, out double warning))
                warningThreshold = warning;
            if (double.TryParse(criticalThresholdBox.Text, out double critical))
                criticalThreshold = critical;

            MessageBox.Show("Thresholds updated successfully.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                voiceCommandStatus.Content = $"Voice Command: {e.Result.Text}";
                if (e.Result.Text == "increase fan speed")
                {
                    IncreaseFanSpeed();
                }
                else if (e.Result.Text == "lower fan speed")
                {
                    LowerFanSpeed();
                }
            });
        }

        private void ToggleVoiceCommand_Click(object sender, RoutedEventArgs e)
        {
            if (isVoiceCommandActive)
            {
                recognizer.RecognizeAsyncStop();
                voiceCommandStatus.Content = "Voice Command: Inactive";
                isVoiceCommandActive = false;
            }
            else
            {
                recognizer.RecognizeAsync(RecognizeMode.Multiple);
                voiceCommandStatus.Content = "Voice Command: Active";
                isVoiceCommandActive = true;
            }
        }

        #endregion

        #region Temperature Monitoring

        private void UpdateTemperatureChart()
        {
            double temp = GetCpuTemperature();
            cpuTemps.Add(temp);

            if (cpuTemps.Count > 50)
                cpuTemps.RemoveAt(0);

            // Update chart axis labels
            cpuChart.AxisX[0].Labels.Add(DateTime.Now.ToString("HH:mm:ss"));
            if (cpuChart.AxisX[0].Labels.Count > 50)
                cpuChart.AxisX[0].Labels.RemoveAt(0);

            // Check thresholds
            if (temp >= criticalThreshold)
            {
                ShowCriticalAlert(temp);
            }
            else if (temp >= warningThreshold)
            {
                ShowWarningAlert(temp);
            }
            else
            {
                // Reset alerts if temperature goes back to normal
                warningAlertShown = false;
                criticalAlertShown = false;
            }
        }

        private double GetCpuTemperature()
        {
            double temp = 0;
            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    hardware.Update();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            temp = sensor.Value.GetValueOrDefault();
                        }
                    }
                }
            }
            return temp;
        }

        #endregion

        #region Process Monitoring

        private void UpdateProcesses()
        {
            var processList = new List<ProcessInfo>();

            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    double cpuUsage = GetCpuUsageForProcess(process);
                    double memoryUsage = process.PrivateMemorySize64 / (1024 * 1024);

                    processList.Add(new ProcessInfo
                    {
                        ProcessName = process.ProcessName,
                        CpuUsage = Math.Round(cpuUsage, 2),
                        MemoryUsage = Math.Round(memoryUsage, 2)
                    });
                }
                catch
                {
                    // Ignore processes that cannot be accessed
                }
            }

            processGrid.ItemsSource = processList.OrderByDescending(p => p.CpuUsage).Take(50);
        }

        // Simple CPU usage simulation for demonstration purposes
        private double GetCpuUsageForProcess(Process process)
        {
            // Implement a proper method if necessary
            return 0; // Placeholder value
        }

        #endregion

        #region Alerts

        private bool warningAlertShown = false;
        private bool criticalAlertShown = false;

        private void ShowWarningAlert(double temp)
        {
            if (!warningAlertShown)
            {
                ShowDesktopAlert($"Warning: CPU temperature has reached {temp}°C.");
                warningAlertShown = true;
            }
        }

        private void ShowCriticalAlert(double temp)
        {
            if (!criticalAlertShown)
            {
                ShowDesktopAlert($"Critical: CPU temperature has reached {temp}°C.");
                criticalAlertShown = true;
            }
        }

        private void ShowDesktopAlert(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "CPU Temperature Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        #endregion

        #region Fan Control Simulation

        private void IncreaseFanSpeed()
        {
            fanSpeedSetting = Math.Min(fanSpeedSetting + 10, 100);
            SetFanSpeed(fanSpeedSetting);
        }

        private void LowerFanSpeed()
        {
            fanSpeedSetting = Math.Max(fanSpeedSetting - 10, 0);
            SetFanSpeed(fanSpeedSetting);
        }

        private void SetFanSpeed(int speedPercentage)
        {
            // Simulate fan speed change in the UI
            fanSpeedLabel.Content = $"Fan Speed: {speedPercentage}% (Simulated)";
        }

        #endregion
    }
}
