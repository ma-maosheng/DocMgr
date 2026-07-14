using DocMgr.Models.YearlyArchive;



namespace DocMgr.Services.Interfaces

{

    /// <summary>

    /// 开柜页单件迁档会话：维护当前拟迁档的档案盒或电子介质袋。

    /// </summary>

    public interface IInteractiveItemRelocationSession

    {

        InteractiveItemRelocationSource? Source { get; }



        event Action? SourceChanged;



        void SetSource(InteractiveItemRelocationSource source);



        void ClearSource();

    }

}

