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
            var list = _topoMapRepository.GetByScale(scale);
            EnsureCurrentMapNumbers(list);
            return list;
        }

        public List<TopoMap> GetAllTopoMaps()
        {
            var list = _topoMapRepository.GetAll();
            EnsureCurrentMapNumbers(list);
            return list;
        }

        public async Task ImportTopoMapsAsync(List<TopoMap> maps, bool isRecreate = false)
        {
            ArgumentNullException.ThrowIfNull(maps);
            foreach (TopoMap map in maps)
            {
                TopoMapCurrentMapNumberSupport.Apply(map);
            }

            await _importSlotGuard.EnsureSlotsReadyForHistoryImportAsync(maps.Select(item => item.BoxNumber));
            _topoMapRepository.Import(maps, isRecreate);
        }

        public void DropTable(string tableName)
        {
            string scale = tableName.Replace("历史存档纸质地形图", "");
            _topoMapRepository.DeleteByScale(scale);
        }

        public void DeleteTopoMap(int id)
        {
            _topoMapRepository.DeleteById(id);
        }

        public void UpdateTopoMap(TopoMap map)
        {
            ArgumentNullException.ThrowIfNull(map);
            TopoMapCurrentMapNumberSupport.Apply(map);
            _topoMapRepository.Update(map);
        }

        private void EnsureCurrentMapNumbers(List<TopoMap> maps)
        {
            if (!TopoMapCurrentMapNumberSupport.FillMissing(maps))
            {
                return;
            }

            _topoMapRepository.SaveChanges();
        }
    }
}