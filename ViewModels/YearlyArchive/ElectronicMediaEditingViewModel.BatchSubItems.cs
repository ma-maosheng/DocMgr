using System;
using System.Threading.Tasks;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive;

public partial class ElectronicMediaEditingViewModel
{
    private async Task BatchAddElectronicMediaItemsAsync(MediaEntryViewModel? media)
    {
        if (!CanEditForm || !AllowElectronicContentScan)
        {
            return;
        }

        await ElectronicMediaBatchSubItemAddSupport.AddAsync(
            media,
            _dialogService,
            _electronicMediaContentScanService,
            CreateDefaultElectronicMediaItem,
            ValidateBatchSubItemFolder,
            ScanBatchSubItemAsync);
    }

    private string? ValidateBatchSubItemFolder(MediaItemViewModel item, string directory)
    {
        return TryEnsureArchiveFilingScannedPathAllowed(item, directory, out string pathError)
            ? null
            : pathError;
    }

    private async Task<string?> ScanBatchSubItemAsync(MediaItemViewModel item, string directory)
    {
        try
        {
            ElectronicMediaContentScanResult result = await Task.Run(() =>
                _electronicMediaContentScanService.ScanDirectories([directory], allowEmpty: true));
            if (!TryEnsureArchiveFilingScannedPathAllowed(item, result.RootPath, out string pathError))
            {
                return pathError;
            }

            ApplyScanResult(item, result, [directory]);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
