using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 开柜页交互式迁档会话：维护当前拟迁档的一件或多件实体（须同档口、同介质轨）。
    /// </summary>
    public interface IInteractiveItemRelocationSession
    {
        /// <summary>当前迁档源列表；空表示未设置。</summary>
        IReadOnlyList<InteractiveItemRelocationSource> Sources { get; }

        /// <summary>兼容单件：列表首项，无源时为 null。</summary>
        InteractiveItemRelocationSource? Source { get; }

        event Action? SourceChanged;

        void SetSource(InteractiveItemRelocationSource source);

        void SetSources(IReadOnlyList<InteractiveItemRelocationSource> sources);

        void ClearSource();
    }
}
