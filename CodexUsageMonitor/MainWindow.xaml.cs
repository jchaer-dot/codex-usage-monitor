using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CodexUsageMonitor;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly Forms.NotifyIcon _notifyIcon;

    private bool _isLocked;
    private string _chartRange = "30d";
    private long? _weeklyResetAt;

    private readonly string _settingsFolder;
    private readonly string _settingsFile;
    private WidgetSettings _settings = new();

    private readonly List<DailyBucket> _dailyBuckets = new();

    public MainWindow()
    {
        InitializeComponent();

        _settingsFolder = IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexUsageMonitor"
        );

        _settingsFile = IOPath.Combine(
            _settingsFolder,
            "settings.json"
        );

        Left = SystemParameters.WorkArea.Right - Width - 12;
        Top = SystemParameters.WorkArea.Top + 12;
        LoadSettings();
        RenderHonestAttribution();
        RenderBalanceGuide();

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "Codex Usage Monitor",
            Visible = true
        };

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };

        _timer.Tick += async (_, _) => { _ = RefreshLocalUsageAsync(); await RefreshAllAsync(); };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        UpdateRangeButtons();
        _settings.LayoutVersion = 2;
        SaveSettings();
        _ = RefreshLocalUsageAsync();
        await RefreshAllAsync();
        _timer.Start();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        SaveSettings();
        _notifyIcon.Dispose();
    }

    // =========================================================
    // HONEST ATTRIBUTION
    // =========================================================

    private void RenderHonestAttribution()
    {
        ModelListPanel.Children.Clear();
        ModelListPanel.Children.Add(CreateInfoText(
            "Leyendo historial local…"));
    }

    private void RenderBalanceGuide()
    {
        ProjectListPanel.Children.Clear();
        ProjectListPanel.Children.Add(CreateInfoText(
            "Esperando saldo semanal…"));
    }

    private TextBlock CreateInfoText(string text) => new()
    {
        Text = text,
        Foreground = Brush("#B5BBC5"),
        FontSize = 10,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = 14
    };

    // =========================================================
    // RESPONSIVE LAYOUT
    // =========================================================

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
        DrawTokenChart();
    }

    private void MainScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
        DrawTokenChart();
    }

    private void ApplyResponsiveLayout()
    {
        if (RootLayout == null) return;
        bool normal = ActualWidth >= 440 && ActualHeight >= 560;
        double inset = normal ? 20 : ActualWidth < 260 ? 10 : 14;
        RootLayout.Margin = new Thickness(inset, 10, inset, 10);
        int[] heights = normal ? new[] {34,90,48,50,0,152,68,28} : new[] {28,74,43,42,0,49,61,25};
        for (int i=0; i<heights.Length; i++)
            if (i!=4) RootLayout.RowDefinitions[i].Height = new GridLength(heights[i]);
        ScaleLabels(RootLayout, normal);
        if (_localUsage != null) RenderLocalRanking();
    }

    private readonly Dictionary<TextBlock, double> _baseFonts = new();
    private void ScaleLabels(DependencyObject parent, bool normal)
    {
        if (parent == ModelListPanel || parent == ProjectListPanel) return;
        if (parent is TextBlock label)
        {
            if (!_baseFonts.TryGetValue(label, out double baseline))
                _baseFonts[label] = baseline = label.FontSize;
            label.FontSize = normal ? baseline * 1.2 : baseline;
        }
        for (int i=0; i<VisualTreeHelper.GetChildrenCount(parent);i++)
            ScaleLabels(VisualTreeHelper.GetChild(parent,i),normal);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isLocked)
            return;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
            }
        }
    }

    private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isLocked)
            return;

        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
    }

    private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isLocked)
            return;

        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private void ResizeCorner_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isLocked)
            return;

        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private void WidgetContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        if (menu.Items.Count == 0)
            return;

        if (menu.Items[0] is not MenuItem item)
            return;

        item.IsChecked = _isLocked;
        item.Header = _isLocked ? "Desbloquear posición" : "Bloquear posición";
    }

    private void ToggleLockMenu_Click(object sender, RoutedEventArgs e)
    {
        _isLocked = !_isLocked;
        SaveSettings();
    }

    private void UseCompactSizeMenu_Click(object sender, RoutedEventArgs e)
    {
        SetWidgetSize(260, 440);
    }

    private void UseNormalSizeMenu_Click(object sender, RoutedEventArgs e)
    {
        SetWidgetSize(520, 620);
    }

    private void SetWidgetSize(double width, double height)
    {
        Width = width;
        Height = height;
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        ApplyResponsiveLayout();
        SaveSettings();
    }

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ManagePlanButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://chatgpt.com/codex/settings/usage",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    // =========================================================
    // SETTINGS
    // =========================================================

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsFile))
                return;

            string json = File.ReadAllText(_settingsFile);
            var settings = JsonSerializer.Deserialize<WidgetSettings>(json);

            if (settings == null)
                return;

            _settings = settings;

            Width = settings.LayoutVersion < 2 ? 260 : Math.Max(MinWidth, settings.Width);
            Height = settings.LayoutVersion < 2 ? 440 : Math.Max(MinHeight, settings.Height);

            var workArea = SystemParameters.WorkArea;

            if (settings.LayoutVersion < 2)
            {
                settings.Left = workArea.Right - Width - 12;
                settings.Top = workArea.Top + 12;
                settings.LayoutVersion = 2;
            }
            Left = Math.Clamp(
                settings.Left,
                workArea.Left,
                Math.Max(workArea.Left, workArea.Right - Width)
            );

            Top = Math.Clamp(
                settings.Top,
                workArea.Top,
                Math.Max(workArea.Top, workArea.Bottom - Height)
            );

            WindowStartupLocation = WindowStartupLocation.Manual;
            _isLocked = settings.Locked;
        }
        catch
        {
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(_settingsFolder);

            _settings.Left = Left;
            _settings.Top = Top;
            _settings.Width = Width;
            _settings.Height = Height;
            _settings.Locked = _isLocked;

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_settingsFile, json);
        }
        catch
        {
        }
    }

    // =========================================================
    // LIVE DATA
    // =========================================================

    private bool _refreshing;
    private async Task RefreshAllAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        Process? process = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try
        {
            string codexPath = Environment.GetEnvironmentVariable("CODEX_WIDGET_CLI") ?? IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "OpenAI",
                "Codex",
                "bin",
                "codex.exe"
            );

            if (!File.Exists(codexPath))
            {
                codexPath = (Environment.GetEnvironmentVariable("PATH") ?? "")
                    .Split(IOPath.PathSeparator).Select(p => IOPath.Combine(p.Trim('"'), "codex.exe"))
                    .FirstOrDefault(File.Exists) ?? "";
                if (codexPath.Length == 0) { SetError("CODEX NOT FOUND"); return; }
            }

            var psi = new ProcessStartInfo
            {
                FileName = codexPath,
                Arguments = "app-server",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            process = Process.Start(psi);

            if (process == null)
            {
                SetError("CONNECTION ERROR");
                return;
            }

            process.StandardInput.AutoFlush = true;

            await process.StandardInput.WriteLineAsync(
                """{"method":"initialize","id":0,"params":{"clientInfo":{"name":"codex_usage_monitor","title":"Codex Usage Monitor","version":"0.5.0"}}}"""
            );

            while (true)
            {
                string? line = await process.StandardOutput.ReadLineAsync(timeout.Token);

                if (line == null)
                    throw new Exception();

                using var doc = JsonDocument.Parse(line);

                if (doc.RootElement.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.Number &&
                    id.GetInt32() == 0)
                {
                    break;
                }
            }

            await process.StandardInput.WriteLineAsync(
                """{"method":"initialized","params":{}}"""
            );

            await process.StandardInput.WriteLineAsync(
                """{"method":"account/rateLimits/read","id":6}"""
            );

            await process.StandardInput.WriteLineAsync(
                """{"method":"account/usage/read","id":7}"""
            );

            JsonElement rateResult = default;
            JsonElement usageResult = default;

            bool gotRate = false;
            bool gotUsage = false;

            while (!gotRate || !gotUsage)
            {
                string? line = await process.StandardOutput.ReadLineAsync(timeout.Token);

                if (line == null)
                    break;

                using var doc = JsonDocument.Parse(line);

                if (!doc.RootElement.TryGetProperty("id", out var idElement))
                    continue;

                if (idElement.ValueKind != JsonValueKind.Number)
                    continue;

                int id = idElement.GetInt32();

                if (id == 6)
                {
                    if (doc.RootElement.TryGetProperty("result", out var result))
                        rateResult = result.Clone();

                    gotRate = true;
                }

                if (id == 7)
                {
                    if (doc.RootElement.TryGetProperty("result", out var result))
                        usageResult = result.Clone();

                    gotUsage = true;
                }
            }

            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
            }

            if (rateResult.ValueKind != JsonValueKind.Undefined)
                UpdateRateLimits(rateResult);
            else SetError("SIN DATOS");

            if (usageResult.ValueKind != JsonValueKind.Undefined)
                UpdateUsage(usageResult);
        }
        catch
        {
            SetError("CONNECTION ERROR");
        }
        finally
        {
            try { if (process != null && !process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            process?.Dispose();
            _refreshing = false;
        }
    }

    private void UpdateRateLimits(JsonElement result)
    {
        if (!result.TryGetProperty("rateLimits", out var rateLimits))
            return;

        double? weeklyUsed = null;
        long? weeklyReset = null;
        double? fiveHourUsed = null;

        ReadWindow(rateLimits, "primary", ref weeklyUsed, ref weeklyReset, ref fiveHourUsed);
        ReadWindow(rateLimits, "secondary", ref weeklyUsed, ref weeklyReset, ref fiveHourUsed);

        WeeklyBar.Value = weeklyUsed.HasValue ? Math.Clamp(100 - weeklyUsed.Value, 0, 100) : 0;
        WeeklyText.Text = weeklyUsed.HasValue ? $"{100 - weeklyUsed.Value:0}%" : "--%";

        FiveHourBar.Value = fiveHourUsed.HasValue ? Math.Clamp(100 - fiveHourUsed.Value, 0, 100) : 0;
        FiveHourText.Text = fiveHourUsed.HasValue ? $"{100 - fiveHourUsed.Value:0}%" : "--%";

        if (rateLimits.TryGetProperty("credits", out var credits) &&
            credits.ValueKind == JsonValueKind.Object &&
            credits.TryGetProperty("balance", out var balance))
        {
            CreditsText.Text = JsonValueToString(balance);
        }
        else
        {
            CreditsText.Text = "—";
        }

        if (weeklyReset.HasValue)
        {
            _weeklyResetAt = weeklyReset;
            var resetTime = DateTimeOffset.FromUnixTimeSeconds(weeklyReset.Value).ToLocalTime();
            var remaining = resetTime - DateTimeOffset.Now;

            if (remaining.TotalSeconds < 0)
                remaining = TimeSpan.Zero;

            if (remaining.TotalDays >= 1)
                ResetText.Text = $"{(int)remaining.TotalDays}d {remaining.Hours}h";
            else
                ResetText.Text = $"{remaining.Hours}h {remaining.Minutes}m";
        }
        else
        {
            ResetText.Text = "--";
        }

        if (!weeklyUsed.HasValue) { SetError("SIN DATOS"); return; }
        double used = weeklyUsed.Value;
        UpdateStatus(used);
        UpdateBalanceGuide(used);
        EvaluateRemainingAlert(used, weeklyReset);
    }

    private void ReadWindow(
        JsonElement rateLimits,
        string propertyName,
        ref double? weeklyUsed,
        ref long? weeklyReset,
        ref double? fiveHourUsed)
    {
        if (!rateLimits.TryGetProperty(propertyName, out var window))
            return;

        if (window.ValueKind != JsonValueKind.Object)
            return;

        if (!window.TryGetProperty("windowDurationMins", out var durationElement))
            return;

        if (!window.TryGetProperty("usedPercent", out var usedElement))
            return;

        int duration = durationElement.GetInt32();
        double used = usedElement.GetDouble();

        if (duration >= 10000)
        {
            weeklyUsed = used;

            if (window.TryGetProperty("resetsAt", out var resetElement))
                weeklyReset = resetElement.GetInt64();
        }

        if (duration >= 290 && duration <= 310)
            fiveHourUsed = used;
    }

    private void UpdateUsage(JsonElement result)
    {
        // summary
        if (result.TryGetProperty("summary", out var summary) &&
            summary.ValueKind == JsonValueKind.Object)
        {
            LifetimeTokensText.Text = ReadSummaryNumber(summary, "lifetimeTokens");
            StreakText.Text = ReadSummaryNumber(summary, "currentStreakDays");
        }

        _dailyBuckets.Clear();

        if (result.TryGetProperty("dailyUsageBuckets", out var buckets) &&
            buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var bucket in buckets.EnumerateArray())
            {
                if (!bucket.TryGetProperty("startDate", out var dateElement))
                    continue;

                if (!bucket.TryGetProperty("tokens", out var tokenElement))
                    continue;

                string? dateString = dateElement.GetString();

                if (!DateTime.TryParse(
                    dateString,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
                    continue;

                double tokens = 0;

                if (tokenElement.ValueKind == JsonValueKind.Number)
                    tokens = tokenElement.GetDouble();

                _dailyBuckets.Add(new DailyBucket
                {
                    Date = date.Date,
                    Tokens = tokens
                });
            }
        }

        _dailyBuckets.Sort((a, b) => a.Date.CompareTo(b.Date));

        if (_dailyBuckets.Count > 0)
        {
            double peak = _dailyBuckets.Max(x => x.Tokens);
            // reuse streak label if summary missing
            if (StreakText.Text == "--")
                StreakText.Text = ComputeSimpleStreak().ToString();

            DrawTokenChart();
        }
        else
        {
            ChartSummaryText.Text = "No usage data returned";
            TokenChart.Children.Clear();
        }
    }

    private int ComputeSimpleStreak()
    {
        if (_dailyBuckets.Count == 0)
            return 0;

        int streak = 0;
        DateTime cursor = _dailyBuckets.Max(x => x.Date);

        var lookup = _dailyBuckets
            .Where(x => x.Tokens > 0)
            .ToDictionary(x => x.Date, x => true);

        while (lookup.ContainsKey(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    // =========================================================
    // CHART
    // =========================================================

    private void Range7Button_Click(object sender, RoutedEventArgs e)
    {
        _chartRange = "7d";
        UpdateRangeButtons();
        DrawTokenChart();
    }

    private void Range30Button_Click(object sender, RoutedEventArgs e)
    {
        _chartRange = "30d";
        UpdateRangeButtons();
        DrawTokenChart();
    }

    private void RangeAllButton_Click(object sender, RoutedEventArgs e)
    {
        _chartRange = "all";
        UpdateRangeButtons();
        DrawTokenChart();
    }

    private void UpdateRangeButtons()
    {
        RenderLocalRanking();
        ApplyRangeButtonStyle(Range7Button, _chartRange == "7d");
        ApplyRangeButtonStyle(Range30Button, _chartRange == "30d");
        ApplyRangeButtonStyle(RangeAllButton, _chartRange == "all");
    }

    private void ApplyRangeButtonStyle(Button button, bool active)
    {
        button.Background = active ? Brush("#4C5370") : Brush("#262A30");
        button.Foreground = active ? Brushes.White : Brush("#C9CED6");
    }

    private void TokenChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawTokenChart();
    }

    private void DrawTokenChart()
    {
        if (TokenChart == null)
            return;

        TokenChart.Children.Clear();

        double width = TokenChart.ActualWidth;
        double height = TokenChart.ActualHeight;

        if (width < 20 || height < 20)
            return;

        List<DailyBucket> values;

        if (_dailyBuckets.Count == 0)
        {
            values = new List<DailyBucket>();
        }
        else if (_chartRange == "7d")
        {
            DateTime end = _dailyBuckets.Max(x => x.Date);
            DateTime start = end.AddDays(-6);
            values = FillRange(start, end);
        }
        else if (_chartRange == "30d")
        {
            DateTime end = _dailyBuckets.Max(x => x.Date);
            DateTime start = end.AddDays(-29);
            values = FillRange(start, end);
        }
        else
        {
            DateTime start = _dailyBuckets.Min(x => x.Date);
            DateTime end = _dailyBuckets.Max(x => x.Date);
            values = FillRange(start, end);
        }

        if (values.Count == 0)
        {
            ChartSummaryText.Text = "Sin actividad disponible";
            return;
        }

        double total = values.Sum(x => x.Tokens);
        double peak = values.Max(x => x.Tokens);

        ChartSummaryText.Text = $"{FormatNumber(total)} tokens / {values.Count}d";

        double labelSpace = 22;
        double usableHeight = height - labelSpace;
        usableHeight = Math.Max(1, usableHeight);

        double gap = values.Count <= 10 ? 10 : values.Count <= 30 ? 4 : 2;
        gap = Math.Min(gap, width / (values.Count * 3));
        double barWidth = Math.Max(0.1, (width - gap * (values.Count - 1)) / values.Count);

        for (int i = 0; i < values.Count; i++)
        {
            double ratio = peak <= 0 ? 0 : values[i].Tokens / peak;
            double barHeight = Math.Max(values[i].Tokens > 0 ? 3 : 0, usableHeight * ratio);

            var rect = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                RadiusX = 3,
                RadiusY = 3,
                Fill = Brush("#818CFF")
            };

            Canvas.SetLeft(rect, i * (barWidth + gap));
            Canvas.SetTop(rect, usableHeight - barHeight);

            TokenChart.Children.Add(rect);
        }

        // first label
        var firstLabel = new TextBlock
        {
            Text = values.First().Date.ToString("d MMM", CultureInfo.InvariantCulture),
            Foreground = Brush("#7E848C"),
            FontSize = 10
        };

        Canvas.SetLeft(firstLabel, 0);
        Canvas.SetTop(firstLabel, usableHeight + 4);
        TokenChart.Children.Add(firstLabel);

        var lastLabel = new TextBlock
        {
            Text = values.Last().Date.ToString("d MMM", CultureInfo.InvariantCulture),
            Foreground = Brush("#7E848C"),
            FontSize = 10
        };

        lastLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Canvas.SetLeft(lastLabel, Math.Max(0, width - lastLabel.DesiredSize.Width));
        Canvas.SetTop(lastLabel, usableHeight + 4);
        TokenChart.Children.Add(lastLabel);
    }

    private List<DailyBucket> FillRange(DateTime start, DateTime end)
    {
        var dict = _dailyBuckets.ToDictionary(x => x.Date, x => x.Tokens);
        var list = new List<DailyBucket>();

        for (DateTime date = start; date <= end; date = date.AddDays(1))
        {
            dict.TryGetValue(date, out double tokens);

            list.Add(new DailyBucket
            {
                Date = date,
                Tokens = tokens
            });
        }

        return list;
    }

    // =========================================================
    // STATUS
    // =========================================================

    private void UpdateStatus(double value)
    {
        if (value >= 100)
        {
            StatusText.Text = "LIMIT REACHED";
            StatusSubtitleText.Text = "Has alcanzado el límite semanal de uso.";
            StatusText.Foreground = Brush("#FF6D6D");
            StatusSubtitleText.Foreground = Brush("#D8A0A0");
            StatusDot.Fill = Brush("#FF5C5C");
            StatusBorder.Background = Brush("#552A1818");
            StatusBorder.BorderBrush = Brush("#55392525");
        }
        else if (value >= 95)
        {
            StatusText.Text = "CRITICAL";
            StatusSubtitleText.Text = "Te queda muy poco margen esta semana.";
            StatusText.Foreground = Brush("#FF8A72");
            StatusSubtitleText.Foreground = Brush("#E4B3A7");
            StatusDot.Fill = Brush("#FF8A72");
            StatusBorder.Background = Brush("#5530241B");
            StatusBorder.BorderBrush = Brush("#55402D24");
        }
        else if (value >= 70)
        {
            StatusText.Text = "WARNING";
            StatusSubtitleText.Text = "El consumo semanal va subiendo rápido.";
            StatusText.Foreground = Brush("#FFD469");
            StatusSubtitleText.Foreground = Brush("#E8D09A");
            StatusDot.Fill = Brush("#FFD469");
            StatusBorder.Background = Brush("#55302A17");
            StatusBorder.BorderBrush = Brush("#55403822");
        }
        else
        {
            StatusText.Text = "NORMAL";
            StatusSubtitleText.Text = "Tu consumo semanal está dentro de rango.";
            StatusText.Foreground = Brush("#8FE3A5");
            StatusSubtitleText.Foreground = Brush("#B8DDBF");
            StatusDot.Fill = Brush("#55C878");
            StatusBorder.Background = Brush("#5518271D");
            StatusBorder.BorderBrush = Brush("#55303F36");
        }
    }

    private void UpdateBalanceGuide(double weeklyUsed)
    {
        double remaining = Math.Clamp(100 - weeklyUsed, 0, 100);
        double days = _weeklyResetAt.HasValue
            ? Math.Max(0.1, (DateTimeOffset.FromUnixTimeSeconds(_weeklyResetAt.Value) - DateTimeOffset.Now).TotalDays) : 7;
        string guidance = $"Reparte ~{remaining / days:0.#} puntos/día hasta el reinicio.";
        ProjectListPanel.Children.Clear();
        ProjectListPanel.Children.Add(CreateInfoText(guidance));
    }

    private void EvaluateRemainingAlert(double weeklyUsed, long? weeklyReset)
    {
        if (!weeklyReset.HasValue)
            return;

        if (weeklyReset.Value != _weeklyResetAt)
        {
            _weeklyResetAt = weeklyReset;
        }

        var settings = LoadAlertSettings();
        if (!settings.WeeklyResetAt.HasValue || Math.Abs(settings.WeeklyResetAt.Value - weeklyReset.Value) > 60)
        {
            settings.WeeklyResetAt = weeklyReset.Value;
            settings.Notified70Remaining = false;
            settings.Notified50Remaining = false;
            settings.Notified30Remaining = false;
        }

        double remaining = Math.Clamp(100 - weeklyUsed, 0, 100);
        int? threshold = remaining <= 30 ? 30 : remaining <= 50 ? 50 : remaining <= 70 ? 70 : null;
        if (!threshold.HasValue)
        {
            PersistAlertSettings(settings);
            return;
        }

        bool alreadyNotified = threshold.Value switch
        {
            70 => settings.Notified70Remaining,
            50 => settings.Notified50Remaining,
            _ => settings.Notified30Remaining
        };

        if (!alreadyNotified)
        {
            _notifyIcon.ShowBalloonTip(
                8000,
                "Codex weekly balance",
                $"{remaining:0}% remains. {GetAlertAdvice(threshold.Value)}",
                Forms.ToolTipIcon.Warning);

            if (threshold.Value == 70) settings.Notified70Remaining = true;
            if (threshold.Value == 50) settings.Notified50Remaining = true;
            if (threshold.Value == 30) settings.Notified30Remaining = true;
        }

        PersistAlertSettings(settings);
    }

    private string GetAlertAdvice(int threshold) => threshold switch
    {
        70 => "Start pacing heavier work.",
        50 => "Reserve Fast and high reasoning for important tasks.",
        _ => "Protect the rest for essential work."
    };

    private WidgetSettings LoadAlertSettings() => _settings;

    private void PersistAlertSettings(WidgetSettings settings)
    {
        _settings = settings;
        SaveSettings();
    }

    private void SetError(string message)
    {
        StatusText.Text = message;
        StatusSubtitleText.Text = "No se pudo leer la información de Codex.";
        StatusText.Foreground = Brush("#FF6D6D");
        StatusSubtitleText.Foreground = Brush("#D8A0A0");
        StatusDot.Fill = Brush("#FF5C5C");
        StatusBorder.Background = Brush("#552A1818");
        StatusBorder.BorderBrush = Brush("#55392525");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private string JsonValueToString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "0",
            JsonValueKind.Number => value.ToString(),
            _ => "0"
        };
    }

    private string ReadSummaryNumber(JsonElement summary, string property)
    {
        if (!summary.TryGetProperty(property, out var value))
            return "--";

        if (value.ValueKind == JsonValueKind.Number)
        {
            double number = value.GetDouble();
            return FormatNumber(number);
        }

        return "--";
    }

    private string FormatNumber(double value)
    {
        if (value >= 1_000_000_000)
            return $"{value / 1_000_000_000.0:0.##}B";

        if (value >= 1_000_000)
            return $"{value / 1_000_000.0:0.##}M";

        if (value >= 1_000)
            return $"{value / 1_000.0:0.#}K";

        return value.ToString("0");
    }

    private SolidColorBrush Brush(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private class DailyBucket
    {
        public DateTime Date { get; set; }
        public double Tokens { get; set; }
    }

    private class WidgetSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public int LayoutVersion { get; set; }
        public double Width { get; set; } = 260;
        public double Height { get; set; } = 440;
        public bool Locked { get; set; }
        public long? WeeklyResetAt { get; set; }
        public bool Notified70Remaining { get; set; }
        public bool Notified50Remaining { get; set; }
        public bool Notified30Remaining { get; set; }
    }
}
