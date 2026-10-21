using System;
using System.Threading.Tasks;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive;

public partial class ArchiveRegisterViewModel
{
    private async Task BatchAddElectronicMediaItemsAsync(MediaEntryViewModel? media)
    {
        if (!CanEditForm)
        {
            return;
        }

        await ElectronicMediaBatchSubItemAddSupport.AddAsync(
            media,
            _dialogService,
            _electronicMediaContentScanService,
            CreateDefaultElectronicMediaItem,
            (_, _) => null,
            ScanBatchSubItemAsync);
    }

    private async Task<string?> ScanBatchSubItemAsync(MediaItemViewModel item, string directory)
    {
        try
        {
            ElectronicMediaContentScanResult result = await Task.Run(() =>
                _electronicMediaContentScanService.ScanDirectories([directory], allowEmpty: true));
            ApplyScanResult(item, result, [directory]);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
