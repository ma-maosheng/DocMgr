using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 档案柜平面布局交互宿主，供布局控件与交互行为按模式区分登记/检索能力。
    /// </summary>
    public interface ICabinetLayoutInteractionHost
    {
        CabinetLayoutWorkspaceMode WorkspaceMode { get; }

        bool AllowOpenOnDoubleClick { get; }

        bool AllowLayoutEdit { get; }

        RelayCommand<Cabinet> SelectCabinetCommand { get; }

        RelayCommand ClearSelectionCommand { get; }

        RelayCommand<Cabinet> SaveLocationCommand { get; }

        RelayCommand<Cabinet>? OpenCabinetCommand { get; }

        RelayCommand<Cabinet>? OpenCabinetFaceACommand { get; }

        RelayCommand<Cabinet>? OpenCabinetFaceBCommand { get; }

        RelayCommand<Cabinet>? RotateCommand { get; }

        RelayCommand? EditCommand { get; }

        RelayCommand<Cabinet>? DeleteCabinetCommand { get; }
    }
}
