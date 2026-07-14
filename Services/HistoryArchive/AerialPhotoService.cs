using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.HistoryArchive
{
    public class AerialPhotoService : IAerialPhotoService
    {
        private readonly IAerialPhotoRepository _aerialPhotoRepository;

        public AerialPhotoService(IAerialPhotoRepository aerialPhotoRepository)
        {
            _aerialPhotoRepository = aerialPhotoRepository;
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

        public void ImportAerialPhotos(List<AerialPhoto> list, string sheetName, bool isRecreate = false)
        {
            string categoryName = $"历史存档航摄影像{sheetName}";
            _aerialPhotoRepository.Import(categoryName, list, isRecreate);
        }

        public void DropTable(string tableName)
        {
            _aerialPhotoRepository.DeleteByCategory(tableName);
        }

        public void UpdateAerialPhoto(AerialPhoto photo)
        {
            _aerialPhotoRepository.Update(photo);
        }
    }
}