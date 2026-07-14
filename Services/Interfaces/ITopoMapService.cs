using System.Collections.Generic;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 地形图资料管理服务契约：地形图成果的录入、查询与维护。
    /// </summary>
    public interface ITopoMapService
    {
        /// <summary>
        /// 检查表是否存在
        /// </summary>
        bool IsTableExist(string tableName);

        /// <summary>
        /// 导入地形图数据
        /// </summary>
        void ImportTopoMaps(List<TopoMap> maps, bool isRecreate = false);

        /// <summary>
        /// 获取所有地形图数据表名
        /// </summary>
        List<string> GetTopoMapTables();

        /// <summary>
        /// 获取指定表中的所有数据
        /// </summary>
        List<TopoMap> GetTopoMapsByTable(string tableName);

        /// <summary>
        /// 删除指定表
        /// </summary>
        void DropTable(string tableName);

        /// <summary>
        /// 更新地形图记录
        /// </summary>
        void UpdateTopoMap(TopoMap map);
    }
}