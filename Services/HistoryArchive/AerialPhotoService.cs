using System.Linq;
using System.Threading.Tasks;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.HistoryArchive
{
    public class AerialPhotoService : IAerialPhotoService
    {
        private readonly IAerialPhotoRepository _aerialPhotoRepository;
        private readonly HistoryArchiveImportSlotGuard _importSlotGuard;

        public AerialPhotoService(
            IAerialPhotoRepository aerialPhotoRepository,
            HistoryArchiveImportSlotGuard importSlotGuard)
        {
            _aerialPhotoRepository = aerialPhotoRepository;
            _importSlotGuard = importSlotGuard;
        }

        public bool IsTableExist(string tableName)
        {
            return _aerialPhotoRepository.ExistsByCategory(tableName);
        }

        public List<string> GetAerialPhotoTables()
        {
            return _aerialPhotoRepository.GetDistinctCategories();
        }

        public List<AerialPhoto> GetAerialPhotosByTable(string tableName)
        {
            return _aerialPhotoRepository.GetByCategory(tableName);
        }

        public List<AerialPhoto> GetAllAerialPhotos()
        {
            return _aerialPhotoRepository.GetAll();
        }

        public async Task ImportAerialPhotosAsync(List<AerialPhoto> list, string sheetName, bool isRecreate = false)
        {
            ArgumentNullException.ThrowIfNull(list);
            await _importSlotGuard.EnsureSlotsReadyForHistoryImportAsync(list.Select(item => item.BoxNumber));
            string categoryName = HistoryArchiveImportTableNameSupport.BuildAerialPhotoTableName(sheetName);
            _aerialPhotoRepository.Import(categoryName, list, isRecreate);
        }

        public void DropTable(string tableName)
        {
            _aerialPhotoRepository.DeleteByCategory(tableName);
        }

        public void DeleteAerialPhoto(int id)
        {
            _aerialPhotoRepository.DeleteById(id);
        }

        public void UpdateAerialPhoto(AerialPhoto photo)
        {
            _aerialPhotoRepository.Update(photo);
        }
    }
}