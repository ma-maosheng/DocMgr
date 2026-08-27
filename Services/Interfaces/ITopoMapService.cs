using System.Collections.Generic;
using System.Threading.Tasks;

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
        /// 导入地形图数据（先核验落档档口用途）。
        /// </summary>
        Task ImportTopoMapsAsync(List<TopoMap> maps, string sheetName, bool isRecreate = false);

        /// <summary>
        /// 获取所有地形图数据表名
        /// </summary>
        List<string> GetTopoMapTables();

        /// <summary>
        /// 获取指定表中的所有数据
        /// </summary>
        List<TopoMap> GetTopoMapsByTable(string tableName);

        /// <summary>
        /// 获取全部地形图记录（跨分类表）。
        /// </summary>
        List<TopoMap> GetAllTopoMaps();

        /// <summary>
        /// 删除指定表
        /// </summary>
        void DropTable(string tableName);

        /// <summary>
        /// 删除单条地形图记录
        /// </summary>
        void DeleteTopoMap(int id);

        /// <summary>
        /// 更新地形图记录
        /// </summary>
        void UpdateTopoMap(TopoMap map);
    }
}