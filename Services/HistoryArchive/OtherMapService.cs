using System.Collections.Generic;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.HistoryArchive
{
    public class OtherMapService : IOtherMapService
    {
        private readonly IOtherMapRepository _otherMapRepository;

        public OtherMapService(IOtherMapRepository otherMapRepository)
        {
            _otherMapRepository = otherMapRepository;
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

        public void ImportOtherMaps(List<OtherMap> list, string sheetName, bool isRecreate = false)
        {
            string categoryName = $"历史存档其他图件{sheetName}";
            _otherMapRepository.Import(categoryName, list, isRecreate);
        }

        public void DropTable(string tableName)
        {
            _otherMapRepository.DeleteByCategory(tableName);
        }

        public void UpdateOtherMap(OtherMap map)
        {
            _otherMapRepository.Update(map);
        }
    }
}
