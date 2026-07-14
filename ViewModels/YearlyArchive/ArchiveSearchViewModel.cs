using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    public class ArchiveSearchViewModel : ViewModelBase
    {
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IDialogService _dialogService;
        private readonly IProjectService _projectService;

        private bool _isInitialized;

        public event Action<YearlyArchiveRegisterRecord>? ViewDetailRequested;

        public class StatusOption
        {
            public string Label { get; set; } = string.Empty;
            public int Value { get; set; }
        }

        private ObservableCollection<string> _years = new() { "全部年份" };
        public ObservableCollection<string> Years
        {
            get => _years;
            set => SetProperty(ref _years, value);
        }

        private string _selectedYear = "全部年份";
        public string SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (!SetProperty(ref _selectedYear, value))
                {
                    return;
                }

                SelectedProjectId = null;
                LoadProjectOptions();
            }
        }

        private string _searchKeyword = string.Empty;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public ObservableCollection<StatusOption> StatusOptions { get; } =
        [
            new StatusOption { Label = "全部", Value = -1 },
            new StatusOption { Label = "未提交", Value = 0 },
            new StatusOption { Label = "已提交", Value = 1 },
            new StatusOption { Label = "已审批", Value = 2 },
            new StatusOption { Label = "已上传签字件", Value = 3 },
            new StatusOption { Label = "已办结", Value = 4 },
            new StatusOption { Label = "已撤回作废", Value = 5 },
            new StatusOption { Label = "已强制作废", Value = 6 }
        ];

        private int _selectedStatusValue = -1;
        public int SelectedStatusValue
        {
            get => _selectedStatusValue;
            set => SetProperty(ref _selectedStatusValue, value);
        }

        private ObservableCollection<YearlyArchiveRegisterRecord> _searchResults = new();
        public ObservableCollection<YearlyArchiveRegisterRecord> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        private YearlyArchiveRegisterRecord? _selectedResult;
        public YearlyArchiveRegisterRecord? SelectedResult
        {
            get => _selectedResult;
            set => SetProperty(ref _selectedResult, value);
        }

        private int? _selectedProjectId;
        public int? SelectedProjectId
        {
            get => _selectedProjectId;
            set => SetProperty(ref _selectedProjectId, value);
        }

        public ObservableCollection<ProjectFilterOption> ProjectOptions { get; } = new();

        public RelayCommand SearchCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand<YearlyArchiveRegisterRecord> ViewDetailCommand { get; }

        public ArchiveSearchViewModel(
            IArchiveRegisterService archiveRegisterService,
            IDialogService dialogService,
            IProjectService projectService)
        {
            _archiveRegisterService = archiveRegisterService;
            _dialogService = dialogService;
            _projectService = projectService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            ResetCommand = new RelayCommand(async _ => await ResetAsync());
            ViewDetailCommand = new RelayCommand<YearlyArchiveRegisterRecord>(ViewDetail);
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            await LoadYearsAsync();
            await SearchAsync();
        }

        private async Task LoadYearsAsync()
        {
            try
            {
                var yearsList = await _archiveRegisterService.GetExistingYearsAsync();

                Years.Clear();
                Years.Add("全部年份");
                foreach (var y in yearsList)
                {
                    Years.Add(y.ToString());
                }

                SelectedYear = Years.FirstOrDefault(x => x != "全部年份") ?? "全部年份";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载年份失败：{ex.Message}");
            }
        }

        private void LoadProjectOptions()
        {
            try
            {
                ProjectFilterOptionFactory.Reload(ProjectOptions, _projectService, SelectedYear);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载项目列表失败：{ex.Message}");
            }
        }

        private async Task SearchAsync()
        {
            try
            {
                int? year = int.TryParse(SelectedYear, out var y) ? y : null;
                int? status = SelectedStatusValue == -1 ? null : SelectedStatusValue;

                var results = await _archiveRegisterService.SearchRecordsAsync(
                    SearchKeyword?.Trim() ?? string.Empty,
                    year,
                    status,
                    SelectedProjectId);

                SearchResults = new ObservableCollection<YearlyArchiveRegisterRecord>(results);
                SelectedResult = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查询失败：{ex.Message}");
            }
        }

        private async Task ResetAsync()
        {
            SearchKeyword = string.Empty;
            SelectedStatusValue = -1;
            SelectedYear = Years.FirstOrDefault(x => x != "全部年份") ?? "全部年份";
            SelectedProjectId = null;

            await SearchAsync();
        }

        private void ViewDetail(YearlyArchiveRegisterRecord? record)
        {
            if (record == null)
            {
                return;
            }

            SelectedResult = record;
            ViewDetailRequested?.Invoke(record);
        }
    }
}
