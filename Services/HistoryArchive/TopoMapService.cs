using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Models.HistoryArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.HistoryArchive
{
    public class TopoMapService : ITopoMapService
    {
        private readonly ITopoMapRepository _topoMapRepository;
        private readonly HistoryArchiveImportSlotGuard _importSlotGuard;
        private readonly IUserContextService _userContextService;

        public TopoMapService(
            ITopoMapRepository topoMapRepository,
            HistoryArchiveImportSlotGuard importSlotGuard,
            IUserContextService userContextService)
        {
            _topoMapRepository = topoMapRepository;
            _importSlotGuard = importSlotGuard;
            _userContextService = userContextService;
        }

        public bool IsTableExist(string tableName)
        {
            return _topoMapRepository.ExistsByCategory(tableName);
        }

        public List<string> GetTopoMapTables()
        {
            return _topoMapRepository.GetDistinctCategories();
        }

        public List<TopoMap> GetTopoMapsByTable(string tableName)
        {
            var list = _topoMapRepository.GetByCategory(tableName);
            EnsureCurrentMapNumbers(list);
            return list;
        }

        public List<TopoMap> GetAllTopoMaps()
        {
            var list = _topoMapRepository.GetAll();
            EnsureCurrentMapNumbers(list);
            return list;
        }

        public async Task ImportTopoMapsAsync(List<TopoMap> maps, string sheetName, bool isRecreate = false)
        {
            HistoryArchiveLedgerPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            ArgumentNullException.ThrowIfNull(maps);
            foreach (TopoMap map in maps)
            {
                TopoMapCurrentMapNumberSupport.Apply(map);
            }

            await _importSlotGuard.EnsureSlotsReadyForHistoryImportAsync(maps.Select(item => item.BoxNumber));
            string categoryName = HistoryArchiveImportTableNameSupport.BuildTopoMapTableName(sheetName);
            _topoMapRepository.Import(categoryName, maps, isRecreate);
        }

        public void DropTable(string tableName)
        {
            HistoryArchiveLedgerPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            _topoMapRepository.DeleteByCategory(tableName);
        }

        public void DeleteTopoMap(int id)
        {
            HistoryArchiveLedgerPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            _topoMapRepository.DeleteById(id);
        }

        public void UpdateTopoMap(TopoMap map)
        {
            HistoryArchiveLedgerPermissionSupport.EnsureCanMaintain(_userContextService.CurrentUser);
            ArgumentNullException.ThrowIfNull(map);
            TopoMapCurrentMapNumberSupport.Apply(map);
            _topoMapRepository.Update(map);
        }

        private static void EnsureCurrentMapNumbers(List<TopoMap> maps)
        {
            // 浏览查询使用 AsNoTracking，只在内存中补全当前图号，避免数万条跟踪实体在切到全局浏览时卡住界面。
            TopoMapCurrentMapNumberSupport.FillMissing(maps);
        }
    }
}
