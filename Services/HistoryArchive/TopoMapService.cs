using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.HistoryArchive
{
    public class TopoMapService : ITopoMapService
    {
        private readonly ITopoMapRepository _topoMapRepository;
        private readonly HistoryArchiveImportSlotGuard _importSlotGuard;

        public TopoMapService(ITopoMapRepository topoMapRepository, HistoryArchiveImportSlotGuard importSlotGuard)
        {
            _topoMapRepository = topoMapRepository;
            _importSlotGuard = importSlotGuard;
        }

        public bool IsTableExist(string tableName)
        {
            string scale = tableName.Replace("历史存档纸质地形图", "");
            return _topoMapRepository.ExistsByScale(scale);
        }

        public List<string> GetTopoMapTables()
        {
            var scales = _topoMapRepository.GetDistinctScales();

            return scales.Select(s => $"历史存档纸质地形图{s}").ToList();
        }

        public List<TopoMap> GetTopoMapsByTable(string tableName)
        {
            string scale = tableName.Replace("历史存档纸质地形图", "");
            return _topoMapRepository.GetByScale(scale);
        }

        public List<TopoMap> GetAllTopoMaps()
        {
            return _topoMapRepository.GetAll();
        }

        public async Task ImportTopoMapsAsync(List<TopoMap> maps, bool isRecreate = false)
        {
            ArgumentNullException.ThrowIfNull(maps);
            await _importSlotGuard.EnsureSlotsReadyForHistoryImportAsync(maps.Select(item => item.BoxNumber));
            _topoMapRepository.Import(maps, isRecreate);
        }

        public void DropTable(string tableName)
        {
            string scale = tableName.Replace("历史存档纸质地形图", "");
            _topoMapRepository.DeleteByScale(scale);
        }

        public void UpdateTopoMap(TopoMap map)
        {
            _topoMapRepository.Update(map);
        }
    }
}