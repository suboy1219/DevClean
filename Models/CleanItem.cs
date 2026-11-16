using DevClean.Mvvm;
using DevClean.Utilities;

namespace DevClean.Models;

public sealed class CleanItem : ObservableObject
{
    private bool _selected;
    private bool _canSelect;
    private bool _available;
    private bool _isBusy;
    private long _sizeBytes;
    private string _statusText = string.Empty;
    private string _statusKind = string.Empty;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Group { get; init; }
    public int GroupOrder { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public RiskLevel Risk { get; init; }
    public bool RequiresAdmin { get; init; }
    public CleanMode Mode { get; init; }
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();
    public string? CommandFile { get; init; }
    public string? CommandArguments { get; init; }
    public string? RequiresCommand { get; init; }

    public bool IsCommand => Mode == CleanMode.Command;
    public bool IsReportOnly => Mode == CleanMode.ReportOnly;
    public int SortRank => IsReportOnly ? 1 : 0;

    public bool Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public bool CanSelect
    {
        get => _canSelect;
        set => SetProperty(ref _canSelect, value);
    }

    public bool Available
    {
        get => _available;
        set
        {
            if (SetProperty(ref _available, value))
            {
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool HasContent => Available && (SizeBytes > 0 || IsCommand);

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
            {
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    public string StatusKind
    {
        get => _statusKind;
        set => SetProperty(ref _statusKind, value);
    }

    public string SizeText => IsReportOnly
        ? ByteFormat.Size(SizeBytes)
        : IsCommand
            ? "按需"
            : Available
                ? ByteFormat.Size(SizeBytes)
                : "未检测到";

    public string RiskText => Risk switch
    {
        RiskLevel.Safe => "安全",
        RiskLevel.Caution => "谨慎",
        _ => "高风险"
    };

    public string PathText => Paths.Count == 0 ? string.Empty : string.Join("\n", Paths);

    public void ResetState()
    {
        IsBusy = false;
        StatusText = string.Empty;
        StatusKind = string.Empty;
    }

    public override string ToString() => Name;
}
