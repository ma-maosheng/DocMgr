using System.Collections.Generic;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 航摄资料管理服务契约：航摄成果的录入、查询与维护。
    /// </summary>
    public interface IAerialPhotoService
    {
        /// <summary>
        /// 检查指定名称的数据表是否存在
        /// </summary>
        bool IsTableExist(string tableName);

        /// <summary>
        /// 将航摄影像数据导入到数据库
        /// </summary>
        /// <param name="list">要导入的数据列表</param>
        /// <param name="sheetName">Sheet名称（用于生成表名后缀）</param>
        /// <param name="isRecreate">是否重建表（即覆盖旧数据）</param>
        void ImportAerialPhotos(List<AerialPhoto> list, string sheetName, bool isRecreate = false);

        /// <summary>
        /// 删除指定名称的数据表
        /// </summary>
        void DropTable(string tableName);

        /// <summary>
        /// 获取所有相关的航摄影像数据表名
        /// </summary>
        List<string> GetAerialPhotoTables();

        /// <summary>
        /// 查询指定表中的所有航摄影像记录
        /// </summary>
        List<AerialPhoto> GetAerialPhotosByTable(string tableName);

        /// <summary>
        /// 更新航摄影像记录
        /// </summary>
        void UpdateAerialPhoto(AerialPhoto photo);
    }
}
