// stopwatch_cs.cs — спортивный секундомер на C# (WPF)

using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace LapMasterWPF
{
    public partial class MainWindow : Window
    {
        private bool running = false;
        private bool paused = false;
        private long startTime = 0;
        private long elapsed = 0;
        private List<long> laps = new List<long>();
        private long lapStart = 0;
        private bool beepOnLap = true;
        private string soundFile = "default";
        private DispatcherTimer updateTimer;

        // UI
        private Label timeLabel;
        private Label bestLabel, worstLabel, avgLabel, countLabel;
        private DataGrid table;
        private Button startBtn, stopBtn, lapBtn, resetBtn, exportBtn, settingsBtn;
        private TextBlock statusText;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            CreateUI();
            UpdateInfo();
        }

        private void CreateUI()
        {
            Title = "🏃 LapMaster Pro — C#";
            Width = 750;
            Height = 600;
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Дисплей
            timeLabel = new Label { FontSize = 48, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            timeLabel.Content = "00:00:00.000";
            Grid.SetRow(timeLabel, 0);
            grid.Children.Add(timeLabel);

            // Кнопки
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5) };
            startBtn = new Button { Content = "Старт", Width = 80, Background = Brushes.Green, Foreground = Brushes.White };
            stopBtn = new Button { Content = "Стоп", Width = 80, Background = Brushes.Red, Foreground = Brushes.White };
            lapBtn = new Button { Content = "Круг", Width = 80 };
            resetBtn = new Button { Content = "Сброс", Width = 80 };
            btnPanel.Children.Add(startBtn);
            btnPanel.Children.Add(stopBtn);
            btnPanel.Children.Add(lapBtn);
            btnPanel.Children.Add(resetBtn);
            Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            startBtn.Click += (s, e) => Start();
            stopBtn.Click += (s, e) => Stop();
            lapBtn.Click += (s, e) => Lap();
            resetBtn.Click += (s, e) => Reset();

            // Информация
            var infoPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            bestLabel = new Label { Content = "Лучший: --", Margin = new Thickness(5,0,5,0) };
            worstLabel = new Label { Content = "Худший: --", Margin = new Thickness(5,0,5,0) };
            avgLabel = new Label { Content = "Средний: --", Margin = new Thickness(5,0,5,0) };
            countLabel = new Label { Content = "Кругов: 0", Margin = new Thickness(5,0,5,0) };
            infoPanel.Children.Add(bestLabel);
            infoPanel.Children.Add(worstLabel);
            infoPanel.Children.Add(avgLabel);
            infoPanel.Children.Add(countLabel);
            Grid.SetRow(infoPanel, 2);
            grid.Children.Add(infoPanel);

            // Таблица
            table = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true };
            table.Columns.Add(new DataGridTextColumn { Header = "№", Binding = new Binding("Number") });
            table.Columns.Add(new DataGridTextColumn { Header = "Время круга", Binding = new Binding("Time") });
            table.Columns.Add(new DataGridTextColumn { Header = "Отставание", Binding = new Binding("Diff") });
            table.Columns.Add(new DataGridTextColumn { Header = "Скорость (км/ч)", Binding = new Binding("Speed") });
            Grid.SetRow(table, 3);
            grid.Children.Add(table);

            // Нижняя панель
            var bottom = new StackPanel { Orientation = Orientation.Horizontal };
            exportBtn = new Button { Content = "Экспорт CSV", Width = 100 };
            settingsBtn = new Button { Content = "Настройки", Width = 100 };
            bottom.Children.Add(exportBtn);
            bottom.Children.Add(settingsBtn);
            statusText = new TextBlock { Text = "Готов", Margin = new Thickness(10,0,0,0) };
            bottom.Children.Add(statusText);
            Grid.SetRow(bottom, 4);
            grid.Children.Add(bottom);

            exportBtn.Click += (s, e) => ExportCSV();
            settingsBtn.Click += (s, e) => SettingsDialog();

            Content = grid;

            // Горячие клавиши
            this.KeyDown += (s, e) => {
                if (e.Key == System.Windows.Input.Key.Space) StartStopToggle();
                if (e.Key == System.Windows.Input.Key.Enter) Lap();
                if (e.Key == System.Windows.Input.Key.R) Reset();
            };

            stopBtn.IsEnabled = false;
            lapBtn.IsEnabled = false;

            // Таймер
            updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            updateTimer.Tick += (s, e) => UpdateTime();
        }

        private void Start()
        {
            if (!running)
            {
                if (paused)
                {
                    running = true;
                    paused = false;
                    startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - elapsed;
                    statusText.Text = "Возобновлён";
                }
                else
                {
                    running = true;
                    paused = false;
                    startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    elapsed = 0;
                    laps.Clear();
                    lapStart = 0;
                    RefreshTable();
                    statusText.Text = "Запущен";
                }
                startBtn.IsEnabled = false;
                stopBtn.IsEnabled = true;
                lapBtn.IsEnabled = true;
                updateTimer.Start();
            }
        }

        private void Stop()
        {
            if (running)
            {
                running = false;
                paused = true;
                startBtn.IsEnabled = true;
                startBtn.Content = "Возобновить";
                stopBtn.IsEnabled = false;
                lapBtn.IsEnabled = false;
                statusText.Text = "На паузе";
                updateTimer.Stop();
            }
        }

        private void StartStopToggle()
        {
            if (running) Stop();
            else Start();
        }

        private void Lap()
        {
            if (running)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long lapTime;
                if (lapStart == 0)
                {
                    lapTime = now - startTime;
                    lapStart = startTime;
                }
                else
                {
                    lapTime = now - lapStart;
                }
                laps.Add(lapTime);
                lapStart = now;
                if (beepOnLap) PlaySound();
                RefreshTable();
                UpdateInfo();
                statusText.Text = $"Круг {laps.Count} зафиксирован";
            }
        }

        private void Reset()
        {
            running = false;
            paused = false;
            elapsed = 0;
            laps.Clear();
            lapStart = 0;
            startBtn.IsEnabled = true;
            startBtn.Content = "Старт";
            stopBtn.IsEnabled = false;
            lapBtn.IsEnabled = false;
            timeLabel.Content = "00:00:00.000";
            RefreshTable();
            UpdateInfo();
            statusText.Text = "Сброшено";
            updateTimer.Stop();
        }

        private void UpdateTime()
        {
            if (running)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                elapsed = now - startTime;
                timeLabel.Content = FormatTime(elapsed);
            }
        }

        private string FormatTime(long ms)
        {
            long hours = ms / 3600000;
            long minutes = (ms % 3600000) / 60000;
            long seconds = (ms % 60000) / 1000;
            long millis = ms % 1000;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{millis:D3}";
        }

        private string FormatTimeShort(long ms)
        {
            long minutes = ms / 60000;
            long seconds = (ms % 60000) / 1000;
            long millis = ms % 1000;
            return $"{minutes:D2}:{seconds:D2}.{millis:D3}";
        }

        private void RefreshTable()
        {
            var items = new List<LapItem>();
            if (laps.Count > 0)
            {
                long best = long.MaxValue;
                foreach (var t in laps) if (t < best) best = t;
                for (int i = 0; i < laps.Count; i++)
                {
                    long diff = laps[i] - best;
                    string diffStr = diff > 0 ? "+" + FormatTimeShort(diff) : "-";
                    items.Add(new LapItem { Number = i+1, Time = FormatTimeShort(laps[i]), Diff = diffStr, Speed = "0.0" });
                }
            }
            table.ItemsSource = items;
        }

        private void UpdateInfo()
        {
            if (laps.Count > 0)
            {
                long best = long.MaxValue, worst = 0;
                long sum = 0;
                foreach (var t in laps) { if (t < best) best = t; if (t > worst) worst = t; sum += t; }
                double avg = (double)sum / laps.Count;
                bestLabel.Content = "Лучший: " + FormatTimeShort(best);
                worstLabel.Content = "Худший: " + FormatTimeShort(worst);
                avgLabel.Content = "Средний: " + FormatTimeShort((long)avg);
                countLabel.Content = "Кругов: " + laps.Count;
            }
            else
            {
                bestLabel.Content = "Лучший: --";
                worstLabel.Content = "Худший: --";
                avgLabel.Content = "Средний: --";
                countLabel.Content = "Кругов: 0";
            }
        }

        private void PlaySound()
        {
            try
            {
                if (soundFile != "default" && File.Exists(soundFile))
                {
                    using (var player = new SoundPlayer(soundFile))
                    {
                        player.Play();
                    }
                }
                else
                {
                    System.Media.SystemSounds.Beep.Play();
                }
            }
            catch { System.Media.SystemSounds.Beep.Play(); }
        }

        private void ExportCSV()
        {
            if (laps.Count == 0)
            {
                MessageBox.Show("Нет кругов для экспорта");
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV (*.csv)|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(dialog.FileName))
                {
                    writer.WriteLine("Круг,Время(мс),Время(формат)");
                    for (int i = 0; i < laps.Count; i++)
                    {
                        writer.WriteLine($"{i+1},{laps[i]},{FormatTimeShort(laps[i])}");
                    }
                }
                statusText.Text = "Экспортировано в " + System.IO.Path.GetFileName(dialog.FileName);
            }
        }

        private void SettingsDialog()
        {
            var dialog = new Window { Title = "Настройки", Width = 400, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var panel = new StackPanel { Margin = new Thickness(10) };
            var beepBox = new CheckBox { Content = "Включить звук при круге", IsChecked = beepOnLap };
            panel.Children.Add(beepBox);
            var soundPanel = new StackPanel { Orientation = Orientation.Horizontal };
            soundPanel.Children.Add(new Label { Content = "Файл звука:" });
            var soundField = new TextBox { Text = soundFile, Width = 200 };
            soundPanel.Children.Add(soundField);
            var browseBtn = new Button { Content = "Обзор..." };
            browseBtn.Click += (s, e) => {
                var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Audio|*.wav;*.mp3" };
                if (ofd.ShowDialog() == true) soundField.Text = ofd.FileName;
            };
            soundPanel.Children.Add(browseBtn);
            panel.Children.Add(soundPanel);
            var okBtn = new Button { Content = "OK", Width = 80 };
            okBtn.Click += (s, e) => {
                beepOnLap = beepBox.IsChecked ?? true;
                soundFile = soundField.Text;
                SaveSettings();
                dialog.Close();
            };
            panel.Children.Add(okBtn);
            dialog.Content = panel;
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void LoadSettings()
        {
            try
            {
                string[] lines = File.ReadAllLines("settings.txt");
                if (lines.Length >= 2)
                {
                    beepOnLap = bool.Parse(lines[0]);
                    soundFile = lines[1];
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllLines("settings.txt", new[] { beepOnLap.ToString(), soundFile });
            }
            catch { }
        }

        public class LapItem
        {
            public int Number { get; set; }
            public string Time { get; set; }
            public string Diff { get; set; }
            public string Speed { get; set; }
        }

        [STAThread]
        static void Main()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}
