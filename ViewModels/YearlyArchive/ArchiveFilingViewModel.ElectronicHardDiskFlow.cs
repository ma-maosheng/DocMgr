using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质立档中留存硬盘（借出归还/外来登记）与档口推荐相关流程。
    /// </summary>
    public partial class ArchiveFilingViewModel
    {
        private bool EnsureExternalHardDiskRegisteredForRetainedScenario(bool showDialogs)
        {
            if (!IsElectronicHardDiskRetainedScenario)
            {
                return true;
            }

            if (!string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.ExternalHardDiskSourceOption, StringComparison.Ordinal))
            {
                return true;
            }

            if (_registeredExternalHardDisk != null)
            {
                return true;
            }

            const string message = "请先完成外来硬盘登记，再进行档口推荐或提交立档。";
            SetElectronicLocationSuggestion(message, isWarning: true);
            if (showDialogs)
            {
                MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return false;
        }

        private void EnsureBorrowedHardDiskLinkedMediumCodesBeforeSubmit()
        {
            if (!IsElectronicHardDiskRetainedScenario || !IsNewBoxMode)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(ElectronicLinkedMediumCodes))
            {
                return;
            }

            if (!TryGetSingleSelectedBorrowedHardDiskCode(out string? borrowedCode))
            {
                return;
            }

            ApplyBorrowedHardDiskCodeForDirectRetainedFiling(borrowedCode!);
        }

        private PendingExternalHardDiskRegistration? CreatePendingExternalHardDiskSnapshot()
        {
            if (!IsElectronicHardDiskRetainedScenario || _registeredExternalHardDisk == null)
            {
                return null;
            }

            return new PendingExternalHardDiskRegistration
            {
                DiskCode = _registeredExternalHardDisk.DiskCode,
                SerialNumber = _registeredExternalHardDisk.SerialNumber,
                DiskType = _registeredExternalHardDisk.DiskType,
                Brand = _registeredExternalHardDisk.Brand,
                Capacity = _registeredExternalHardDisk.Capacity,
                InterfaceType = _registeredExternalHardDisk.InterfaceType,
                RegisterPerson = _registeredExternalHardDisk.RegisterPerson,
                RegisterDate = _registeredExternalHardDisk.RegisterDate,
                FactoryDate = _registeredExternalHardDisk.FactoryDate,
                RegistrationMethod = _registeredExternalHardDisk.RegistrationMethod,
                CurrentLocation = _registeredExternalHardDisk.CurrentLocation,
                CurrentStatus = _registeredExternalHardDisk.CurrentStatus,
                MediaNature = _registeredExternalHardDisk.MediaNature,
                CurrentHolder = _registeredExternalHardDisk.CurrentHolder,
                NeedReturn = _registeredExternalHardDisk.NeedReturn,
                DataCarrierFormedDate = _registeredExternalHardDisk.DataCarrierFormedDate,
                DataDescription = _registeredExternalHardDisk.DataDescription,
                RelatedBatch = _registeredExternalHardDisk.RelatedBatch,
                TransferTarget = _registeredExternalHardDisk.TransferTarget,
                TransferDate = _registeredExternalHardDisk.TransferDate,
                Remark = _registeredExternalHardDisk.Remark,
                FormattedBlankTargetLocation = ExternalHardDiskFormattedBlankTargetLocation.Trim()
            };
        }

        private HardDiskMediaReturnCandidate? CreateBorrowedHardDiskCandidateSnapshot()
        {
            if (_borrowedHardDiskReturnCandidate == null)
            {
                return null;
            }

            if (IsElectronicHardDiskRetainedScenario && IsNewBoxMode
                && !string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.BorrowedHardDiskSourceOption, StringComparison.Ordinal))
            {
                return null;
            }

            return _borrowedHardDiskReturnCandidate with { };
        }

        private async Task PrefillElectronicFieldsFromSelectedRecordsAsync()
        {
            var selectedElectronicItems = EnumerateSelectedElectronicMediaEntryRows().ToList();

            if (selectedElectronicItems.Count == 0)
            {
                ElectronicSourceCarrierSummary = string.Empty;
                ElectronicSourceStoragePathSummary = string.Empty;
                IsRetainedHardDiskScenario = false;
                _borrowedHardDiskReturnCandidate = null;
                SelectedRetainedHardDiskSource = string.Empty;
                _registeredExternalHardDisk = null;
                OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
                if (IsNewBoxMode)
                {
                    ElectronicStoragePath = string.Empty;
                    ElectronicDisposition = string.Empty;
                    ElectronicContentSummary = string.Empty;
                }

                SelectedHardDiskCopyTargetMode = string.Empty;
                RaiseElectronicStepFourPresentationChanged();
                return;
            }

            var selectedMediaTypes = selectedElectronicItems
                .Select(item => item.MediaType?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToList();

            if (selectedMediaTypes.Count > 1)
            {
                throw new InvalidOperationException("电子介质立档一次只能处理同一种介质类型的资料，请重新勾选。");
            }

            ElectronicSourceCarrierSummary = string.Join(" / ", selectedElectronicItems
                .Select(media => media.MediaType)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct());
            ElectronicSourceStoragePathSummary = string.Join("；", selectedElectronicItems
                .Select(media => media.StoragePath)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct());

            string sourceDisposition = string.Join(" / ", selectedElectronicItems
                .Select(media => media.Disposition)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct());
            string sourceItemNameSummary = string.Join("；", selectedElectronicItems
                .Select(item => item.ItemName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct());

            if (IsNewBoxMode)
            {
                ElectronicStoragePath = ElectronicSourceStoragePathSummary;
                ElectronicDisposition = sourceDisposition;
                ElectronicContentSummary = sourceItemNameSummary;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ElectronicDisposition))
                {
                    ElectronicDisposition = sourceDisposition;
                }

                if (string.IsNullOrWhiteSpace(ElectronicContentSummary))
                {
                    ElectronicContentSummary = sourceItemNameSummary;
                }
            }

            RefreshElectronicScenario();

            IsRetainedHardDiskScenario = IsElectronicHardDiskRetainedScenario;
            ElectronicStorageCarrierType = _electronicDecision.StorageCarrierType;

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew)
            {
                ElectronicLinkedMediumCodes = string.Empty;
                ElectronicOriginalStorageLocation = string.Empty;
                ElectronicSelectedMediumStatus = string.Empty;
                _borrowedHardDiskReturnCandidate = null;
                SelectedRetainedHardDiskSource = string.Empty;
                _registeredExternalHardDisk = null;
                ExternalHardDiskFormattedBlankTargetLocation = string.Empty;
                OnPropertyChanged(nameof(RegisteredExternalHardDiskCodeDisplay));
                OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
                ElectronicMediaCount = 1;
                return;
            }

            if (IsElectronicCopyScenario && !IsElectronicHardDiskRetainedScenario)
            {
                SelectedRetainedHardDiskSource = string.Empty;
                _borrowedHardDiskReturnCandidate = null;
                _registeredExternalHardDisk = null;
                ExternalHardDiskFormattedBlankTargetLocation = string.Empty;
                OnPropertyChanged(nameof(RegisteredExternalHardDiskCodeDisplay));
                OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));

                ApplySelectedSubmissionMode();
                EnsureElectronicBagDefaults();

                return;
            }

            if (!IsElectronicHardDiskRetainedScenario)
            {
                _borrowedHardDiskReturnCandidate = null;
                SelectedRetainedHardDiskSource = string.Empty;
                _registeredExternalHardDisk = null;
                ExternalHardDiskFormattedBlankTargetLocation = string.Empty;
                OnPropertyChanged(nameof(RegisteredExternalHardDiskCodeDisplay));
                OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
                return;
            }

            bool fromApplicationBorrowedDirectRetained =
                SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew
                && selectedElectronicItems.Count == 1
                && selectedElectronicItems[0].IsBorrowedHardDisk
                && !string.IsNullOrWhiteSpace(selectedElectronicItems[0].BorrowedHardDiskCode);

            if (fromApplicationBorrowedDirectRetained)
            {
                ElectronicLinkedMediumCodes = selectedElectronicItems[0].BorrowedHardDiskCode!.Trim();
            }

            await RefreshBorrowedHardDiskCandidatesAsync();

            ApplyInferredRetainedHardDiskSourceForRefresh();
            ApplyRetainedHardDiskSourceSelection();

            ApplySelectedSubmissionMode();

            EnsureElectronicBagDefaults();
            await InitializeExternalHardDiskFormattedBlankTargetLocationAsync();
            RaiseElectronicStepFourPresentationChanged();
        }

        private async Task RefreshBorrowedHardDiskCandidatesAsync()
        {
            var candidates = await _hardDiskMediaService.GetReturnRegistrationCandidatesAsync();
            var orderedCandidates = candidates
                .OrderBy(item => item.ApplicantDept, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ApplicantName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? matchDiskCode = null;
            if (IsElectronicHardDiskRetainedScenario)
            {
                var borrowedRows = EnumeratePendingElectronicMediaItemRows()
                    .Where(item => item.IsBorrowedHardDisk && !string.IsNullOrWhiteSpace(item.BorrowedHardDiskCode))
                    .Select(item => item.BorrowedHardDiskCode!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (borrowedRows.Count == 1)
                {
                    matchDiskCode = borrowedRows[0];
                }
            }

            if (string.IsNullOrWhiteSpace(matchDiskCode) && !string.IsNullOrWhiteSpace(ElectronicLinkedMediumCodes))
            {
                matchDiskCode = ElectronicLinkedMediumCodes.Trim();
            }

            var matchedCandidate = orderedCandidates.FirstOrDefault(item => _borrowedHardDiskReturnCandidate != null && item.MediumId == _borrowedHardDiskReturnCandidate.MediumId)
                ?? (string.IsNullOrWhiteSpace(matchDiskCode)
                    ? null
                    : orderedCandidates.FirstOrDefault(item => string.Equals(item.DiskCode, matchDiskCode, StringComparison.OrdinalIgnoreCase)));

            if (matchedCandidate == null && !string.IsNullOrWhiteSpace(matchDiskCode))
            {
                matchedCandidate = await _hardDiskMediaService.GetReturnRegistrationCandidateByDiskCodeAsync(matchDiskCode);
            }

            _borrowedHardDiskReturnCandidate = matchedCandidate;
            ApplySelectedBorrowedHardDiskCandidate();
            RaiseElectronicStepFourPresentationChanged();
        }

        private void SetElectronicLocationSuggestion(string message, bool isWarning)
        {
            ElectronicLocationSuggestionHint = message;
            ElectronicLocationSuggestionBrush = isWarning
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F855A"));
        }

        private string BuildElectronicArchiveSuccessMessage(ElectronicArchiveSubmissionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            string summary = result.IsAppendMode
                ? $"电子介质已并入电子立档单元：{result.ElectronicArchiveNo}\n本次已入袋 {result.MediaEntryCount} 个电子介质条目。"
                : $"电子介质立档成功。\n电子袋编号：{result.ElectronicArchiveNo}\n本次已入袋 {result.MediaEntryCount} 个电子介质条目。";

            if (!IsElectronicHardDiskRetainedScenario || !IsNewBoxMode)
            {
                return summary;
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew)
            {
                return summary + "\n当前留存硬盘已直接作为电子立档介质保存。";
            }

            if (string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.BorrowedHardDiskSourceOption, StringComparison.Ordinal)
                && _borrowedHardDiskReturnCandidate != null)
            {
                return summary + $"\n留存硬盘 [{_borrowedHardDiskReturnCandidate.DiskCode}] 已按资料立档归还入库。";
            }

            if (string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.ExternalHardDiskSourceOption, StringComparison.Ordinal)
                && _registeredExternalHardDisk != null)
            {
                return summary + $"\n外来硬盘 [{_registeredExternalHardDisk.DiskCode}] 已登记入库并关联到当前电子袋。";
            }

            return summary;
        }

        private void ShowElectronicArchiveResultDialog(ElectronicArchiveSubmissionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            string summary = BuildElectronicArchiveSuccessMessage(result);
            string content = result.DatabaseChanges != null
                ? result.DatabaseChanges.ToDialogText(summary)
                : summary;

            _dialogService.ShowTextDetailDialog(content, "电子介质立档 · 数据库变更明细");
        }

        private void ShowElectronicArchivePreviewDialog(ElectronicArchiveSubmissionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            string summary = BuildElectronicArchivePreviewMessage(result);
            string content = result.DatabaseChanges != null
                ? result.DatabaseChanges.ToPreviewDialogText(summary)
                : summary;

            _dialogService.ShowTextDetailDialog(content, "电子介质立档 · 拟执行逻辑预览");
        }

        private string BuildElectronicArchivePreviewMessage(ElectronicArchiveSubmissionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            string summary = result.IsAppendMode
                ? $"【预览】拟将电子介质并入电子立档单元：{result.ElectronicArchiveNo}\n本次拟入袋 {result.MediaEntryCount} 个电子介质条目。"
                : $"【预览】拟执行电子介质立档。\n电子袋编号：{result.ElectronicArchiveNo}\n本次拟入袋 {result.MediaEntryCount} 个电子介质条目。";

            if (!IsElectronicHardDiskRetainedScenario || !IsNewBoxMode)
            {
                return summary + "\n\n以下内容为拟执行逻辑，尚未写入数据库。";
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew)
            {
                return summary + "\n当前留存硬盘将直接作为电子立档介质保存。\n\n以下内容为拟执行逻辑，尚未写入数据库。";
            }

            if (string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.BorrowedHardDiskSourceOption, StringComparison.Ordinal)
                && _borrowedHardDiskReturnCandidate != null)
            {
                string targetLocationHint = ResolveBorrowedRetainedHardDiskPreviewTargetLocationHint(_borrowedHardDiskReturnCandidate);

                if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc)
                {
                    return summary + $"\n留存借出硬盘 [{_borrowedHardDiskReturnCandidate.DiskCode}] 将在资料拷贝至光盘立档后执行格式化，并归还至 {targetLocationHint}。\n\n以下内容为拟执行逻辑，尚未写入数据库。";
                }

                return summary + $"\n留存硬盘 [{_borrowedHardDiskReturnCandidate.DiskCode}] 将按资料立档归还入库（目标位置：{targetLocationHint}）。\n\n以下内容为拟执行逻辑，尚未写入数据库。";
            }

            if (string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.ExternalHardDiskSourceOption, StringComparison.Ordinal)
                && _registeredExternalHardDisk != null)
            {
                return summary + $"\n外来硬盘 [{_registeredExternalHardDisk.DiskCode}] 将登记入库并关联到当前电子袋。\n\n以下内容为拟执行逻辑，尚未写入数据库。";
            }

            return summary + "\n\n以下内容为拟执行逻辑，尚未写入数据库。";
        }

        private static string ResolveBorrowedRetainedHardDiskPreviewTargetLocationHint(HardDiskMediaReturnCandidate candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate.OriginalLocation))
            {
                return $"原存放档口 [{candidate.OriginalLocation.Trim()}]";
            }

            if (!string.IsNullOrWhiteSpace(candidate.BorrowedLocation))
            {
                return $"借出时位置 [{candidate.BorrowedLocation.Trim()}]";
            }

            return "空白硬盘专用档口";
        }

        private void ApplyRetainedHardDiskSourceSelection()
        {
            if (!IsElectronicHardDiskRetainedScenario || !IsNewBoxMode)
            {
                return;
            }

            if (string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.BorrowedHardDiskSourceOption, StringComparison.Ordinal))
            {
                if (_borrowedHardDiskReturnCandidate != null)
                {
                    ApplySelectedBorrowedHardDiskCandidate();
                }
                else if (SelectedElectronicSubmissionMode != ElectronicArchiveSubmissionMode.CopyNewHardDisk)
                {
                    if (TryGetSingleSelectedBorrowedHardDiskCode(out string? borrowedCode))
                    {
                        ApplyBorrowedHardDiskCodeForDirectRetainedFiling(borrowedCode!);
                    }
                    else
                    {
                        ClearSelectedHardDisk();
                    }
                }

                return;
            }

            if (string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.ExternalHardDiskSourceOption, StringComparison.Ordinal))
            {
                if (_registeredExternalHardDisk != null)
                {
                    ApplyExternalHardDisk(_registeredExternalHardDisk);
                }
                else
                {
                    ClearSelectedHardDisk();
                }
            }
        }

        private void ApplySelectedBorrowedHardDiskCandidate()
        {
            if (_borrowedHardDiskReturnCandidate == null)
            {
                return;
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk)
            {
                return;
            }

            if (!IsElectronicHardDiskRetainedScenario || !IsNewBoxMode)
            {
                return;
            }

            if (!string.Equals(SelectedRetainedHardDiskSource, ArchiveFilingBusinessRules.BorrowedHardDiskSourceOption, StringComparison.Ordinal))
            {
                return;
            }

            EnsureElectronicBagDefaults();
            ElectronicLinkedMediumCodes = _borrowedHardDiskReturnCandidate.DiskCode;
            ElectronicOriginalStorageLocation = string.IsNullOrWhiteSpace(_borrowedHardDiskReturnCandidate.BorrowedLocation)
                ? _borrowedHardDiskReturnCandidate.OriginalLocation
                : _borrowedHardDiskReturnCandidate.BorrowedLocation;
            ElectronicSelectedMediumStatus = _borrowedHardDiskReturnCandidate.CurrentStatus;
            ElectronicMediaCount = 1;
            ElectronicLocationSuggestionHint = string.Empty;
        }

        private void ApplyExternalHardDisk(PendingExternalHardDiskRegistration medium)
        {
            EnsureElectronicBagDefaults();
            ElectronicLinkedMediumCodes = medium.DiskCode;
            ElectronicOriginalStorageLocation = medium.CurrentLocation;
            ElectronicSelectedMediumStatus = medium.CurrentStatus;
            ElectronicMediaCount = 1;
            ElectronicLocationSuggestionHint = string.Empty;

            if (string.IsNullOrWhiteSpace(ElectronicContentSummary) && !string.IsNullOrWhiteSpace(medium.DataDescription))
            {
                ElectronicContentSummary = medium.DataDescription;
            }

            OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
        }

        private void ApplyBorrowedHardDiskCodeForDirectRetainedFiling(string borrowedHardDiskCode)
        {
            if (string.IsNullOrWhiteSpace(borrowedHardDiskCode))
            {
                return;
            }

            EnsureElectronicBagDefaults();
            ElectronicLinkedMediumCodes = borrowedHardDiskCode.Trim();
            ElectronicMediaCount = 1;
            ElectronicLocationSuggestionHint = string.Empty;
        }

        private bool TryGetSingleSelectedBorrowedHardDiskCode(out string? borrowedHardDiskCode)
        {
            borrowedHardDiskCode = null;

            var codes = EnumeratePendingElectronicMediaItemRows()
                .Where(item => item.IsBorrowedHardDisk && !string.IsNullOrWhiteSpace(item.BorrowedHardDiskCode))
                .Select(item => item.BorrowedHardDiskCode.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count != 1)
            {
                return false;
            }

            borrowedHardDiskCode = codes[0];
            return true;
        }

        private void ClearSelectedHardDisk()
        {
            ElectronicLinkedMediumCodes = string.Empty;
            ElectronicOriginalStorageLocation = string.Empty;
            ElectronicSelectedMediumStatus = string.Empty;
            ElectronicLocationSuggestionHint = string.Empty;
            OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
        }

        private async Task RegisterExternalHardDiskAsync()
        {
            var medium = _registeredExternalHardDisk != null
                ? CreateHardDiskMediumFromPendingExternalRegistration(_registeredExternalHardDisk)
                : CreateDefaultHardDiskMediumForExternalRegistration(_userContextService.CurrentUser?.RealName ?? string.Empty);

            if (!_dialogService.ShowHardDiskMediumEditDialog(medium, persistOnConfirm: false))
            {
                return;
            }

            _registeredExternalHardDisk = CreateExternalHardDiskRegistration(medium);
            SelectedRetainedHardDiskSource = ArchiveFilingBusinessRules.ExternalHardDiskSourceOption;
            if (IsNewBoxMode)
            {
                ApplyExternalHardDisk(_registeredExternalHardDisk);
            }

            await InitializeExternalHardDiskFormattedBlankTargetLocationAsync();
            OnPropertyChanged(nameof(RegisteredExternalHardDiskCodeDisplay));

            string suggestionMessage = string.Empty;
            if (IsNewBoxMode)
            {
                await SuggestElectronicLocationAsync(showDialogs: false);
                if (!string.IsNullOrWhiteSpace(ElectronicStorageLocation))
                {
                    suggestionMessage = $"\n已按资料硬盘规则建议档口：{ElectronicStorageLocation}";
                }

                if (!string.IsNullOrWhiteSpace(ExternalHardDiskFormattedBlankTargetLocation))
                {
                    suggestionMessage += $"\n已建议格式化后空盘入库档口：{ExternalHardDiskFormattedBlankTargetLocation}";
                }
            }

            _dialogService.ShowMessage($"外来硬盘 [{medium.DiskCode}] 登记信息已暂存到当前电子介质立档操作台，待执行电子介质立档时再同步入库。{suggestionMessage}", "提示");
        }

        private async Task RecommendExternalHardDiskBlankTargetLocationAsync()
        {
            if (ExternalHardDiskFormattedBlankLocationVisibility != Visibility.Visible)
            {
                _dialogService.ShowMessage("当前场景无需为外来硬盘执行格式化后空盘入库推荐。", "提示");
                return;
            }

            await InitializeExternalHardDiskFormattedBlankTargetLocationAsync();
            if (string.IsNullOrWhiteSpace(ExternalHardDiskFormattedBlankTargetLocation))
            {
                _dialogService.ShowMessage("未找到空白硬盘专用档口，请先在磁盘柜开柜界面完成设置。", "提示");
                return;
            }

            _dialogService.ShowMessage($"已建议格式化后空盘入库档口：{ExternalHardDiskFormattedBlankTargetLocation}", "建议档口位置");
        }

        private async Task InitializeExternalHardDiskFormattedBlankTargetLocationAsync()
        {
            if (ExternalHardDiskFormattedBlankLocationVisibility != Visibility.Visible)
            {
                ExternalHardDiskFormattedBlankTargetLocation = string.Empty;
                return;
            }

            try
            {
                ExternalHardDiskFormattedBlankTargetLocation =
                    await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync() ?? string.Empty;
            }
            catch
            {
                ExternalHardDiskFormattedBlankTargetLocation = string.Empty;
            }
        }

        private void ResetElectronicFields()
        {
            ElectronicArchiveNo = string.Empty;
            ElectronicStorageCarrierType = string.Empty;
            ElectronicSourceCarrierSummary = string.Empty;
            ElectronicSourceStoragePathSummary = string.Empty;
            ElectronicStoragePath = string.Empty;
            ElectronicStorageLocation = string.Empty;
            ElectronicOriginalStorageLocation = string.Empty;
            ElectronicSelectedMediumStatus = string.Empty;
            ElectronicLinkedMediumCodes = string.Empty;
            ElectronicDisposition = string.Empty;
            ElectronicMediaCount = 0;
            ElectronicContentSummary = string.Empty;
            ElectronicCellCountText = "-";
            IsRetainedHardDiskScenario = false;
            _borrowedHardDiskReturnCandidate = null;
            SelectedRetainedHardDiskSource = string.Empty;
            _registeredExternalHardDisk = null;
            ExternalHardDiskFormattedBlankTargetLocation = string.Empty;
            ElectronicLocationSuggestionHint = string.Empty;
            OnPropertyChanged(nameof(RegisteredExternalHardDiskCodeDisplay));
            OnPropertyChanged(nameof(ExternalHardDiskRegistrationTooltip));
        }

        private static HardDiskMedium CreateDefaultHardDiskMediumForExternalRegistration(string registerPerson)
        {
            return new HardDiskMedium
            {
                RegisterDate = DateTime.Today,
                RegisterPerson = registerPerson,
                RegistrationMethod = HardDiskMedium.RegistrationMethodArchive,
                Ledger = new HardDiskLedger
                {
                    MediaStatus = HardDiskMedium.StatusInStockData,
                    MediaNature = HardDiskMedium.NatureDataCarrier,
                    HolderOrOrganization = "资料室",
                    StorageLocation = string.Empty,
                    NeedReturn = false,
                    RegisterDate = DateTime.Today,
                    RegisterPerson = registerPerson,
                    Remark = string.Empty,
                    CreatedTime = DateTime.Today,
                    UpdatedTime = DateTime.Today
                }
            };
        }

        private static HardDiskMedium CreateHardDiskMediumFromPendingExternalRegistration(PendingExternalHardDiskRegistration pending)
        {
            ArgumentNullException.ThrowIfNull(pending);

            DateTime registerDate = pending.RegisterDate == default ? DateTime.Today : pending.RegisterDate;
            string registerPerson = pending.RegisterPerson.Trim();

            return new HardDiskMedium
            {
                DiskCode = pending.DiskCode.Trim(),
                SerialNumber = pending.SerialNumber.Trim(),
                DiskType = pending.DiskType.Trim(),
                Brand = pending.Brand.Trim(),
                Capacity = pending.Capacity.Trim(),
                InterfaceType = pending.InterfaceType.Trim(),
                RegisterPerson = registerPerson,
                RegisterDate = registerDate,
                FactoryDate = pending.FactoryDate,
                RegistrationMethod = string.IsNullOrWhiteSpace(pending.RegistrationMethod)
                    ? HardDiskMedium.RegistrationMethodArchive
                    : pending.RegistrationMethod.Trim(),
                Remark = pending.Remark.Trim(),
                Ledger = new HardDiskLedger
                {
                    DiskCode = pending.DiskCode.Trim(),
                    MediaStatus = string.IsNullOrWhiteSpace(pending.CurrentStatus)
                        ? HardDiskMedium.StatusInStockData
                        : pending.CurrentStatus.Trim(),
                    MediaNature = string.IsNullOrWhiteSpace(pending.MediaNature)
                        ? HardDiskMedium.NatureDataCarrier
                        : pending.MediaNature.Trim(),
                    StorageLocation = pending.CurrentLocation.Trim(),
                    HolderOrOrganization = string.IsNullOrWhiteSpace(pending.CurrentHolder)
                        ? "资料室"
                        : pending.CurrentHolder.Trim(),
                    NeedReturn = pending.NeedReturn,
                    RegisterDate = registerDate,
                    RegisterPerson = registerPerson,
                    Remark = pending.Remark.Trim(),
                    CreatedTime = DateTime.Today,
                    UpdatedTime = DateTime.Today
                }
            };
        }

        private static PendingExternalHardDiskRegistration CreateExternalHardDiskRegistration(HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(medium);

            var ledger = medium.Ledger;

            return new PendingExternalHardDiskRegistration
            {
                DiskCode = medium.DiskCode,
                SerialNumber = medium.SerialNumber,
                DiskType = medium.DiskType,
                Brand = medium.Brand,
                Capacity = medium.Capacity,
                InterfaceType = medium.InterfaceType,
                RegisterPerson = medium.RegisterPerson,
                RegisterDate = medium.RegisterDate,
                FactoryDate = medium.FactoryDate,
                RegistrationMethod = medium.RegistrationMethod,
                CurrentLocation = ledger?.StorageLocation ?? string.Empty,
                CurrentStatus = ledger?.MediaStatus ?? string.Empty,
                MediaNature = ledger?.MediaNature ?? string.Empty,
                CurrentHolder = ledger?.HolderOrOrganization ?? string.Empty,
                NeedReturn = ledger?.NeedReturn ?? false,
                DataCarrierFormedDate = null,
                DataDescription = string.Empty,
                RelatedBatch = string.Empty,
                TransferTarget = string.Empty,
                TransferDate = null,
                Remark = medium.Remark
            };
        }

        private string BuildExternalHardDiskRegistrationTooltip()
        {
            if (_registeredExternalHardDisk == null)
            {
                return "请先登记外来留存硬盘，登记成功后自动带入硬盘编号。";
            }

            return string.Join(Environment.NewLine,
                $"硬盘编号：{_registeredExternalHardDisk.DiskCode}",
                $"序列号：{_registeredExternalHardDisk.SerialNumber}",
                $"硬盘类型：{_registeredExternalHardDisk.DiskType}",
                $"品牌/容量：{_registeredExternalHardDisk.Brand} / {_registeredExternalHardDisk.Capacity}",
                $"接口类型：{_registeredExternalHardDisk.InterfaceType}",
                $"出厂日期：{FormatOptionalDate(_registeredExternalHardDisk.FactoryDate)}",
                $"登记方式：{_registeredExternalHardDisk.RegistrationMethod}",
                $"当前位置：{_registeredExternalHardDisk.CurrentLocation}",
                $"当前状态：{_registeredExternalHardDisk.CurrentStatus}",
                $"介质属性：{_registeredExternalHardDisk.MediaNature}",
                $"保管单位：{_registeredExternalHardDisk.CurrentHolder}",
                $"资料说明：{_registeredExternalHardDisk.DataDescription}",
                $"备注：{_registeredExternalHardDisk.Remark}");
        }

        private async Task EnsureOpticalDiscAppendTargetCompatibleAsync()
        {
            if (IsNewBoxMode)
            {
                return;
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew)
            {
                throw new InvalidOperationException("光盘留存场景每张光盘需单独立档，不允许并档。");
            }

            if (!CanUseElectronicAppendMode)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(_electronicDecision.AppendRestrictionReason)
                    ? "当前场景不允许并档，请使用新建立档。"
                    : _electronicDecision.AppendRestrictionReason);
            }

            if (SelectedExistingElectronicUnit == null)
            {
                throw new InvalidOperationException("请先选择要并入的电子介质袋。");
            }

            if (!ArchiveFilingBusinessRules.IsHardDiskArchiveCarrierType(SelectedExistingElectronicUnit.StorageCarrierType))
            {
                throw new InvalidOperationException($"当前选中的电子介质袋 [{SelectedExistingElectronicUnit.ElectronicArchiveNo}] 不是硬盘档，当前业务只允许并入本项目已立档硬盘。");
            }
        }

        private void SelectElectronicMedia()
        {
            if (!_electronicDecision.StepFourLayout.ShowBlankInventoryHardDiskSelection)
            {
                _dialogService.ShowMessage("当前立档方式无需从硬盘库选择空白硬盘作为入袋载体。", "提示");
                return;
            }

            int? currentElectronicArchiveUnitId = IsNewBoxMode ? null : SelectedExistingElectronicUnit?.Id;
            var selectedMedia = _dialogService.ShowHardDiskMediumSelectionDialog(
                ParseLinkedMediumCodes(ElectronicLinkedMediumCodes),
                currentElectronicArchiveUnitId,
                ResolveHardDiskSelectionMode());
            if (selectedMedia == null || selectedMedia.Count == 0)
            {
                return;
            }

            if (selectedMedia.Count > 1)
            {
                _dialogService.ShowMessage("电子介质袋一次只能关联一块硬盘，请只选择一块作为入袋/拷贝目标硬盘。", "提示");
                return;
            }

            var targetMedium = selectedMedia[0];

            if (!IsNewBoxMode
                && SelectedExistingElectronicUnit != null
                && !string.IsNullOrWhiteSpace(SelectedExistingElectronicUnit.LinkedMediumCodes)
                && !string.Equals(SelectedExistingElectronicUnit.LinkedMediumCodes.Trim(), targetMedium.DiskCode, StringComparison.Ordinal))
            {
                _dialogService.ShowMessage($"当前电子介质袋已关联硬盘 [{SelectedExistingElectronicUnit.LinkedMediumCodes}]，并入时不能改为其他硬盘。", "提示");
                return;
            }

            EnsureElectronicBagDefaults();
            ElectronicLinkedMediumCodes = targetMedium.DiskCode;
            ElectronicOriginalStorageLocation = targetMedium.Ledger?.StorageLocation ?? string.Empty;
            ElectronicSelectedMediumStatus = targetMedium.Ledger?.MediaStatus ?? string.Empty;
            ElectronicMediaCount = 1;
        }

        private void ResetElectronicLocationSelection()
        {
            SelectedElectronicCabinet = null;
            SelectedElectronicSide = string.Empty;
            SelectedElectronicRow = string.Empty;
            SelectedElectronicColumn = string.Empty;
            ReplaceItems(ElectronicSides, Array.Empty<string>());
            ReplaceItems(ElectronicRows, Array.Empty<string>());
            ReplaceItems(ElectronicColumns, Array.Empty<string>());
            ElectronicStorageLocation = string.Empty;
            ElectronicCellCountText = "-";
            _currentElectronicCellMediumCount = 0;
            if (ElectronicCabinets.Count > 0)
            {
                SelectedElectronicCabinet = ElectronicCabinets[0];
            }
        }

        private void UpdateElectronicSides()
        {
            ReplaceItems(ElectronicSides, Array.Empty<string>());
            if (SelectedElectronicCabinet == null)
            {
                return;
            }

            ElectronicSides.Add("A");
            if (SelectedElectronicCabinet.FaceCount > 1)
            {
                ElectronicSides.Add("B");
            }

            if (ElectronicSides.Count > 0)
            {
                SelectedElectronicSide = ElectronicSides[0];
            }
        }

        private void UpdateElectronicRowsAndCols()
        {
            ReplaceItems(ElectronicRows, Array.Empty<string>());
            ReplaceItems(ElectronicColumns, Array.Empty<string>());
            if (SelectedElectronicCabinet == null)
            {
                return;
            }

            for (int i = 1; i <= SelectedElectronicCabinet.LayerCount; i++)
            {
                ElectronicRows.Add(i.ToString());
            }

            for (int i = 1; i <= SelectedElectronicCabinet.ColumnCount; i++)
            {
                ElectronicColumns.Add(i.ToString());
            }

            if (ElectronicRows.Count > 0)
            {
                SelectedElectronicRow = ElectronicRows[0];
            }

            if (ElectronicColumns.Count > 0)
            {
                SelectedElectronicColumn = ElectronicColumns[0];
            }
        }

        private async void CalculateElectronicLocation()
        {
            if (!IsElectronicTrack || !IsNewBoxMode)
            {
                return;
            }

            if (SelectedElectronicCabinet == null
                || !int.TryParse(SelectedElectronicRow, out int row)
                || !int.TryParse(SelectedElectronicColumn, out int column)
                || string.IsNullOrWhiteSpace(SelectedElectronicSide))
            {
                ElectronicStorageLocation = string.Empty;
                ElectronicCellCountText = "-";
                _currentElectronicCellMediumCount = 0;
                return;
            }

            try
            {
                _currentElectronicCellMediumCount = await _filingService.GetElectronicUnitCountInCellAsync(
                    SelectedElectronicCabinet.Name,
                    SelectedElectronicSide,
                    row,
                    column);

                ElectronicStorageLocation = $"{SelectedElectronicCabinet.Name}{SelectedElectronicSide}-{row}-{column}-{_currentElectronicCellMediumCount + 1:D2}";
                ElectronicCellCountText = $"{_currentElectronicCellMediumCount} 袋";
            }
            catch (Exception ex)
            {
                ElectronicStorageLocation = string.Empty;
                ElectronicCellCountText = "位置计算失败";
                MessageBox.Show("计算电子介质档口位置失败: " + ex.Message);
            }
        }

        private async Task SuggestElectronicLocationAsync()
            => await SuggestElectronicLocationAsync(showDialogs: true);

        private async Task SuggestElectronicLocationAsync(bool showDialogs)
        {
            if (!IsElectronicTrack || !IsNewBoxMode)
            {
                return;
            }

            if (!EnsureExternalHardDiskRegisteredForRetainedScenario(showDialogs))
            {
                return;
            }

            if (UsesOpticalDiscCarrierForLabels)
            {
                await SuggestElectronicLocationByCategoryAsync(CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc, "年度数据光盘专用", showDialogs);
                return;
            }

            if (SelectedElectronicSubmissionMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk
                && string.IsNullOrWhiteSpace(ElectronicLinkedMediumCodes))
            {
                SetElectronicLocationSuggestion("请先选择入袋/拷贝目标硬盘，再进行档口推荐。", isWarning: true);
                if (showDialogs)
                {
                    MessageBox.Show("请先选择入袋/拷贝目标硬盘。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            string categoryName = ResolveElectronicSlotCategoryName();
            await SuggestElectronicLocationByCategoryAsync(categoryName, categoryName == CabinetHardDiskSlotCategoryAssignment.CategoryDamaged ? "损坏硬盘专用" : "年度数据硬盘专用", showDialogs);
        }

        private async Task SuggestElectronicLocationByCategoryAsync(string categoryName, string categoryDisplayName, bool showDialogs)
        {
            var options = await _hardDiskMediaService.GetDedicatedTargetLocationOptionsAsync(categoryName);
            if (options.Count == 0)
            {
                SetElectronicLocationSuggestion($"未找到“{categoryDisplayName}档口”，请先在磁盘柜开柜界面完成设置。", isWarning: true);
                if (showDialogs)
                {
                    MessageBox.Show($"请先在磁盘柜开柜界面设置“{categoryDisplayName}档口”。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            var suggestedOption = options
                .OrderBy(item => item.ExistingMediumCount)
                .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
                .First();

            if (!TryApplyElectronicSlotCode(suggestedOption.Location))
            {
                SetElectronicLocationSuggestion($"建议档口 [{suggestedOption.Location}] 解析失败，请手动选择位置。", isWarning: true);
                if (showDialogs)
                {
                    MessageBox.Show($"建议档口 [{suggestedOption.Location}] 解析失败，请手动选择位置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            SetElectronicLocationSuggestion($"已建议使用{categoryDisplayName}档口 {suggestedOption.Location}。", isWarning: false);
            if (showDialogs)
            {
                MessageBox.Show($"建议使用{categoryDisplayName}档口 {suggestedOption.Location}。", "建议档口位置", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool TryApplyElectronicSlotCode(string? slotCode)
        {
            if (string.IsNullOrWhiteSpace(slotCode))
            {
                return false;
            }

            string trimmedSlotCode = slotCode.Trim();
            var parts = trimmedSlotCode.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            string cabinetAndSide = parts[0];
            if (cabinetAndSide.Length < 2)
            {
                return false;
            }

            string side = cabinetAndSide[^1].ToString();
            string cabinetName = cabinetAndSide[..^1];
            string row = parts[1];
            string column = parts[2];

            var matchedCabinet = ElectronicCabinets.FirstOrDefault(item => string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (matchedCabinet == null)
            {
                return false;
            }

            SelectedElectronicCabinet = matchedCabinet;
            SelectedElectronicSide = side;
            SelectedElectronicRow = row;
            SelectedElectronicColumn = column;
            return true;
        }

        private void ShowElectronicSlotSnapshot()
        {
            if (!IsElectronicTrack)
            {
                return;
            }

            if (TryResolveElectronicSlotSnapshotContext(out Cabinet cabinet, out string side, out string row, out string column))
            {
                ShowSlotSnapshot(cabinet, side, row, column);
                return;
            }

            _dialogService.ShowMessage("当前暂无有效物理存放位置，无法查看档口占用快照。", "提示");
        }

        private void ShowExternalHardDiskBlankTargetSlotSnapshot()
        {
            if (!IsElectronicTrack)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ExternalHardDiskFormattedBlankTargetLocation))
            {
                _dialogService.ShowMessage("请先生成空白硬盘入库推荐档口。", "提示");
                return;
            }

            if (!TryShowElectronicSlotSnapshotByLocation(ExternalHardDiskFormattedBlankTargetLocation))
            {
                _dialogService.ShowMessage("当前推荐档口解析失败，请重新生成推荐档口后重试。", "提示");
            }
        }

        private bool TryShowElectronicSlotSnapshotByLocation(string? location) =>
            TryShowSlotSnapshotByLocation(location, ElectronicCabinets);

        private string ResolveElectronicSlotCategoryName()
        {
            return string.Equals(ElectronicSelectedMediumStatus, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal)
                ? CabinetHardDiskSlotCategoryAssignment.CategoryDamaged
                : CabinetHardDiskSlotCategoryAssignment.CategoryData;
        }

        private void EnsureElectronicBagDefaults()
        {
            if (UsesOpticalDiscCarrierForLabels)
            {
                if (string.IsNullOrWhiteSpace(ElectronicStorageCarrierType))
                {
                    ElectronicStorageCarrierType = ArchiveFilingBusinessRules.DefaultOpticalDiscBagCarrierType;
                }

                ElectronicMediaCount = 1;

                return;
            }

            if (string.IsNullOrWhiteSpace(ElectronicStorageCarrierType))
            {
                ElectronicStorageCarrierType = ArchiveFilingBusinessRules.DefaultElectronicBagCarrierType;
            }

            if (ElectronicMediaCount <= 0)
            {
                ElectronicMediaCount = 1;
            }
        }

        private static int ResolveElectronicMediaCount(string? linkedMediumCodes, int fallbackCount)
        {
            int linkedCount = ParseLinkedMediumCodes(linkedMediumCodes).Take(2).Count();
            if (linkedCount > 0)
            {
                return linkedCount;
            }

            return fallbackCount > 0 ? fallbackCount : 0;
        }

        private static IEnumerable<string> ParseLinkedMediumCodes(string? linkedMediumCodes)
        {
            if (string.IsNullOrWhiteSpace(linkedMediumCodes))
            {
                return Enumerable.Empty<string>();
            }

            return linkedMediumCodes
                .Split([',', '，', ';', '；', '\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal);
        }

        private string? ResolveHardDiskSelectionMode()
        {
            if (SelectedElectronicSubmissionMode != ElectronicArchiveSubmissionMode.CopyNewHardDisk)
            {
                return null;
            }

            string resolvedMode = _filingService.ResolveHardDiskSelectionMode(ArchiveFilingBusinessRules.BlankHardDiskSourceOption);
            return string.IsNullOrWhiteSpace(resolvedMode) ? null : resolvedMode;
        }

        private static string FormatOptionalDate(DateTime? value)
            => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "—";
    }
}
