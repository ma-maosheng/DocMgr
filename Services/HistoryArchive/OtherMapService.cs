using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.HistoryArchive
{
    public class OtherMapService : IOtherMapService
    {
        private readonly IOtherMapRepository _otherMapRepository;
        private readonly HistoryArchiveImportSlotGuard _importSlotGuard;

        public OtherMapService(IOtherMapRepository otherMapRepository, HistoryArchiveImportSlotGuard importSlotGuard)
        {
            _otherMapRepository = otherMapRepository;
            _importSlotGuard = importSlotGuard;
        }

        public bool IsTableExist(string tableName)
        {
            return _otherMapRepository.ExistsByCategory(tableName);
        }

        public List<string> GetOtherMapTables()
        {
            return _otherMapRepository.GetDistinctCategories();
        }

        public List<OtherMap> GetOtherMapsByTable(string tableName)
        {
            return _otherMapRepository.GetByCategory(tableName);
        }

        public List<OtherMap> GetAllOtherMaps()
        {
            return _otherMapRepository.GetAll();
        }

        public async Task ImportOtherMapsAsync(List<OtherMap> list, string sheetName, bool isRecreate = false)
        {
            ArgumentNullException.ThrowIfNull(list);
            await _importSlotGuard.EnsureSlotsReadyForHistoryImportAsync(list.Select(item => item.BoxNumber));
            string categoryName = HistoryArchiveImportTableNameSupport.BuildOtherMapTableName(sheetName);
            _otherMapRepository.Import(categoryName, list, isRecreate);
        }

        public void DropTable(string tableName)
        {
            _otherMapRepository.DeleteByCategory(tableName);
        }

        public void DeleteOtherMap(int id)
        {
            _otherMapRepository.DeleteById(id);
        }

        public void UpdateOtherMap(OtherMap map)
        {
            _otherMapRepository.Update(map);
        }
    }
}
