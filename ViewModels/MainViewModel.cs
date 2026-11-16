using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using DevClean.Models;
using DevClean.Mvvm;
using DevClean.Services;
using DevClean.Utilities;
using DevClean.Views;

namespace DevClean.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly CancellationTokenSource _cancellation = new();
    private CancellationTokenSource? _scanCancellation;

    private bool _isScanning;
    private bool _isCleaning;
    private bool _showOnlyAvailable = true;
    private bool _batchSelection;
    private double _scanProgress;
    private string _scanText = string.Empty;
    private string _statusText = "准备就绪";
    private long _totalReclaimableBytes;
    private long _selectedBytes;
    private int _availableCount;
    private int _selectedCount;
    private string _driveName = "C:";
    private string _driveFreeText = "—";
    private string _driveTotalText = "—";
    private double _driveUsedPercent;

    public MainViewModel(bool demo = false)
    {
        IsDemo = demo;
        Items = new ObservableCollection<CleanItem>(demo ? DemoCatalog.Create() : CleanupCatalog.Create());
        foreach (CleanItem item in Items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CleanItem.Group)));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(CleanItem.GroupOrder), ListSortDirection.Ascending));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(CleanItem.SortRank), ListSortDirection.Ascending));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(CleanItem.SizeBytes), ListSortDirection.Descending));
        ItemsView.Filter = FilterItem;

        ScanCommand = new AsyncRelayCommand(ScanAsync, CanStartWork);
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => CanStartWork() && SelectedCount > 0);
        SelectSafeCommand = new RelayCommand(SelectSafeItems, CanStartWork);
        ClearSelectionCommand = new RelayCommand(ClearSelection, CanStartWork);
        ElevateCommand = new RelayCommand(Elevate);
        OpenItemCommand = new RelayCommand<CleanItem>(OpenItem);

        if (demo)
        {
            SetDemoDriveInfo();
            UpdateSelectability();
            AutoSelectRecommended();
            ItemsView.Refresh();
            RecalculateTotals();
            StatusText = $"演示数据 · 已自动勾选 {SelectedCount} 个建议项 · {SelectedText}";
        }
        else
        {
            UpdateDriveInfo();
        }
    }

    public ObservableCollection<CleanItem> Items { get; }

    public ICollectionView ItemsView { get; }

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand CleanCommand { get; }
    public RelayCommand SelectSafeCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand ElevateCommand { get; }
    public RelayCommand<CleanItem> OpenItemCommand { get; }

    public bool IsElevated => Elevation.IsElevated;
    public bool IsDemo { get; }
    public bool ShowElevationBanner => !IsElevated && !IsDemo;
    public bool ShowDemoBanner => IsDemo;
    public string SafetyNote => IsDemo
        ? "当前为演示数据，不会读取或删除任何本机文件。"
        : "仅清理白名单缓存；个人文档与代码不会被改动；占用中的文件会自动跳过。";

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                NotifyCommands();
            }
        }
    }

    public bool IsCleaning
    {
        get => _isCleaning;
        private set
        {
            if (SetProperty(ref _isCleaning, value))
            {
                NotifyCommands();
            }
        }
    }

    public bool ShowOnlyAvailable
    {
        get => _showOnlyAvailable;
        set
        {
            if (SetProperty(ref _showOnlyAvailable, value))
            {
                ItemsView.Refresh();
            }
        }
    }

    public double ScanProgress
    {
        get => _scanProgress;
        private set => SetProperty(ref _scanProgress, value);
    }

    public string ScanText
    {
        get => _scanText;
        private set => SetProperty(ref _scanText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public long TotalReclaimableBytes
    {
        get => _totalReclaimableBytes;
        private set
        {
            if (SetProperty(ref _totalReclaimableBytes, value))
            {
                OnPropertyChanged(nameof(TotalReclaimableText));
            }
        }
    }

    public string TotalReclaimableText => TotalReclaimableBytes <= 0 ? "0 B" : ByteFormat.Size(TotalReclaimableBytes);

    public string TotalReclaimableSubText => $"检测到 {AvailableCount} 项可处理";

    public long SelectedBytes
    {
        get => _selectedBytes;
        private set
        {
            if (SetProperty(ref _selectedBytes, value))
            {
                OnPropertyChanged(nameof(SelectedText));
                OnPropertyChanged(nameof(SelectedSummary));
            }
        }
    }

    public string SelectedText => SelectedBytes <= 0 ? "0 B" : ByteFormat.Size(SelectedBytes);

    public int AvailableCount
    {
        get => _availableCount;
        private set
        {
            if (SetProperty(ref _availableCount, value))
            {
                OnPropertyChanged(nameof(TotalReclaimableSubText));
            }
        }
    }

    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                OnPropertyChanged(nameof(SelectedSummary));
                CleanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedSummary => SelectedCount == 0
        ? "尚未选择清理项"
        : $"已选 {SelectedCount} 项 · {SelectedText}";

    public string DriveName
    {
        get => _driveName;
        private set => SetProperty(ref _driveName, value);
    }

    public string DriveFreeText
    {
        get => _driveFreeText;
        private set => SetProperty(ref _driveFreeText, value);
    }

    public string DriveTotalText
    {
        get => _driveTotalText;
        private set => SetProperty(ref _driveTotalText, value);
    }

    public double DriveUsedPercent
    {
        get => _driveUsedPercent;
        private set
        {
            if (SetProperty(ref _driveUsedPercent, value))
            {
                OnPropertyChanged(nameof(DriveDetailText));
            }
        }
    }

    public string DriveDetailText => $"共 {DriveTotalText} · 已用 {DriveUsedPercent:0}%";

    public async Task InitializeAsync()
    {
        if (IsDemo)
        {
            return;
        }

        UpdateDriveInfo();
        await ScanAsync();
    }

    public void Cancel()
    {
        _scanCancellation?.Cancel();
        _cancellation.Cancel();
    }

    private bool CanStartWork() => !IsScanning && !IsCleaning;

    private bool FilterItem(object obj)
    {
        if (obj is not CleanItem item)
        {
            return false;
        }

        return !ShowOnlyAvailable || item.Available || item.HasStatus;
    }

    private async Task ScanAsync()
    {
        if (IsDemo)
        {
            AutoSelectRecommended();
            StatusText = $"演示数据 · 已重新勾选 {SelectedCount} 个建议项 · {SelectedText}";
            return;
        }

        if (!CanStartWork())
        {
            return;
        }

        _scanCancellation?.Dispose();
        _scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
        CancellationToken token = _scanCancellation.Token;

        IsScanning = true;
        StatusText = "正在扫描磁盘，请稍候…";
        ScanProgress = 0;
        ScanText = $"0 / {Items.Count}";
        ItemsView.Refresh();

        try
        {
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                ScanProgress = p.Total == 0 ? 0 : p.Done * 100.0 / p.Total;
                ScanText = $"{p.Done} / {p.Total}";
            });

            await SizeScanner.ScanAsync(Items, progress, token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
            return;
        }
        catch (Exception ex)
        {
            AppLog.Write("SCAN error: " + ex);
            StatusText = "扫描失败：" + ex.Message;
            return;
        }
        finally
        {
            IsScanning = false;
            UpdateSelectability();
            AutoSelectRecommended();
            UpdateDriveInfo();
            ItemsView.Refresh();
            RecalculateTotals();
        }

        StatusText = AvailableCount == 0
            ? "未检测到可清理项"
            : SelectedCount > 0
                ? $"扫描完成 · 已自动勾选 {SelectedCount} 个建议项 · {SelectedText}"
                : $"扫描完成 · 可清理 {TotalReclaimableText} · 未发现建议项";
    }

    private void UpdateSelectability()
    {
        foreach (CleanItem item in Items)
        {
            bool canSelect = !item.IsReportOnly
                             && item.Available
                             && (!item.RequiresAdmin || IsElevated);
            item.CanSelect = canSelect;

            if (!canSelect)
            {
                item.Selected = false;
            }
        }
    }

    private async Task CleanAsync()
    {
        if (!CanStartWork())
        {
            return;
        }

        List<CleanItem> targets = Items.Where(i => i.Selected && i.CanSelect).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("请先勾选需要清理的项目。", "DevClean", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long expected = targets.Sum(i => i.SizeBytes);
        var dialog = new ConfirmDialog(targets, expected)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (IsDemo)
        {
            MessageBox.Show("当前是演示模式，不会执行任何删除操作。\n\n使用发布目录中的 DevClean.exe 正常启动即可清理真实缓存。",
                "DevClean 演示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsCleaning = true;
        StatusText = "正在清理选中的项目…";
        long freedTotal = 0;
        var problems = new List<CleanItem>();
        int successCount = 0;

        AppLog.Write($"CLEAN batch start items={targets.Count} expected={expected}");

        try
        {
            foreach (CleanItem item in targets)
            {
                if (_cancellation.IsCancellationRequested)
                {
                    break;
                }

                item.IsBusy = true;
                item.StatusText = "正在清理…";
                item.StatusKind = "busy";
                StatusText = $"正在清理：{item.Name}";

                CleanOutcome outcome;
                try
                {
                    outcome = await Cleaner.CleanAsync(item, _cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    item.StatusText = "已取消";
                    item.StatusKind = "warn";
                    break;
                }
                catch (Exception ex)
                {
                    AppLog.Write($"CLEAN item failed {item.Id}: {ex}");
                    outcome = new CleanOutcome(false, 0, 0, 0, ex.Message);
                }

                freedTotal += outcome.FreedBytes;
                item.IsBusy = false;
                item.Selected = false;

                if (outcome.Success)
                {
                    successCount++;
                    item.StatusText = outcome.FreedBytes > 0
                        ? $"已清理 · 释放 {ByteFormat.Size(outcome.FreedBytes)}"
                        : "已执行";
                    item.StatusKind = "ok";
                }
                else
                {
                    problems.Add(item);
                    item.StatusText = Shorten(outcome.Detail, 48);
                    item.StatusKind = "warn";
                }

                if (item.Mode is CleanMode.DeleteContents or CleanMode.DeleteTree or CleanMode.DeleteFiles)
                {
                    long residual = await Task.Run(() =>
                        PathResolver.ExpandAll(item.Paths).Sum(PathResolver.GetSize), _cancellation.Token);
                    item.SizeBytes = residual;
                    item.Available = residual > 0;
                }
            }
        }
        finally
        {
            IsCleaning = false;
            UpdateSelectability();
            UpdateDriveInfo();
            ItemsView.Refresh();
            RecalculateTotals();
        }

        AppLog.Write($"CLEAN batch done freed={freedTotal} success={successCount} problems={problems.Count}");

        StatusText = problems.Count == 0
            ? $"清理完成 · 本次释放 {ByteFormat.Size(freedTotal)}"
            : $"清理完成 · 释放 {ByteFormat.Size(freedTotal)} · {problems.Count} 项未完全清理";

        string message = $"清理完成\n\n本次释放：{ByteFormat.Size(freedTotal)}\n成功项目：{successCount} 项";
        if (problems.Count > 0)
        {
            message += $"\n未能完全清理：{problems.Count} 项\n\n" +
                       string.Join("\n", problems.Take(6).Select(p => $"· {p.Name}：{Shorten(p.StatusText, 60)}"));
            if (problems.Count > 6)
            {
                message += $"\n· 其余 {problems.Count - 6} 项见清单状态";
            }

            message += "\n\n占用中的文件可以关闭对应程序后再次清理。";
        }

        MessageBox.Show(message, "DevClean", MessageBoxButton.OK,
            problems.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void SelectSafeItems()
    {
        AutoSelectRecommended();
        StatusText = SelectedCount == 0 ? "没有可勾选的安全项" : "已勾选所有建议清理项";
    }

    private void AutoSelectRecommended()
    {
        _batchSelection = true;
        try
        {
            foreach (CleanItem item in Items)
            {
                item.Selected = item.CanSelect && item.Risk == RiskLevel.Safe;
            }
        }
        finally
        {
            _batchSelection = false;
        }

        RecalculateSelection();
    }

    private void ClearSelection()
    {
        foreach (CleanItem item in Items)
        {
            item.Selected = false;
        }

        RecalculateSelection();
        StatusText = "已清空选择";
    }

    private void Elevate()
    {
        if (IsDemo)
        {
            MessageBox.Show("演示模式不会请求管理员权限。", "DevClean 演示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsElevated)
        {
            return;
        }

        if (!Elevation.RelaunchElevated())
        {
            MessageBox.Show("无法自动提权，请右键 DevClean.exe 选择「以管理员身份运行」。",
                "DevClean", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Application.Current.Shutdown();
    }

    public void OpenItem(CleanItem? item)
    {
        if (IsDemo)
        {
            MessageBox.Show("演示模式不会打开本机目录。", "DevClean 演示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (item is null)
        {
            return;
        }

        string? path = PathResolver.ExpandAll(item.Paths).FirstOrDefault();
        try
        {
            if (path is not null && File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }

            if (path is not null && Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
                return;
            }

            MessageBox.Show("该项目当前没有对应的本地目录（可能尚未生成，或已被清理）。",
                "DevClean", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开目录：" + ex.Message, "DevClean",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanItem.Selected) && !_batchSelection)
        {
            RecalculateSelection();
        }
    }

    private void RecalculateTotals()
    {
        AvailableCount = Items.Count(i => i.Available && !i.IsReportOnly);
        TotalReclaimableBytes = Items
            .Where(i => i.Available && !i.IsReportOnly && !i.IsCommand)
            .Sum(i => i.SizeBytes);
        RecalculateSelection();
    }

    private void RecalculateSelection()
    {
        SelectedCount = Items.Count(i => i.Selected && i.CanSelect);
        SelectedBytes = Items.Where(i => i.Selected && i.CanSelect).Sum(i => i.SizeBytes);
        OnPropertyChanged(nameof(SelectedText));
        OnPropertyChanged(nameof(SelectedSummary));
    }

    private void UpdateDriveInfo()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            DriveName = drive.Name.TrimEnd('\\');
            DriveFreeText = ByteFormat.Size(drive.AvailableFreeSpace);
            DriveTotalText = ByteFormat.Size(drive.TotalSize);
            DriveUsedPercent = drive.TotalSize <= 0
                ? 0
                : (drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize;
        }
        catch (Exception ex)
        {
            AppLog.Write("DRIVE info error: " + ex.Message);
        }
    }

    private void SetDemoDriveInfo()
    {
        const long total = 512L * 1024 * 1024 * 1024;
        const long free = (long)(96.4 * 1024 * 1024 * 1024);
        DriveName = "C:";
        DriveTotalText = ByteFormat.Size(total);
        DriveFreeText = ByteFormat.Size(free);
        DriveUsedPercent = (total - free) * 100.0 / total;
    }

    private void NotifyCommands()
    {
        ScanCommand.RaiseCanExecuteChanged();
        CleanCommand.RaiseCanExecuteChanged();
        SelectSafeCommand.RaiseCanExecuteChanged();
        ClearSelectionCommand.RaiseCanExecuteChanged();
    }

    private static string Shorten(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";
}
