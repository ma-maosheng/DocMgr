using System.Collections.Generic;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 其它图件资料管理服务契约：非标准图件成果的录入、查询与维护。
    /// </summary>
    public interface IOtherMapService
    {
        /// <summary>
        /// 检查指定名称的数据表是否存在。
        /// </summary>
        bool IsTableExist(string tableName);

        /// <summary>
        /// 将其他图件数据导入到数据库。
        /// </summary>
        void ImportOtherMaps(List<OtherMap> list, string sheetName, bool isRecreate = false);

        /// <summary>
        /// 删除指定名称的数据表。
        /// </summary>
        void DropTable(string tableName);

        /// <summary>
        /// 获取所有其他图件数据表名。
        /// </summary>
        List<string> GetOtherMapTables();

        /// <summary>
        /// 查询指定表中的所有其他图件记录。
        /// </summary>
        List<OtherMap> GetOtherMapsByTable(string tableName);

        /// <summary>
        /// 更新其他图件记录。
        /// </summary>
        void UpdateOtherMap(OtherMap map);
    }
}
