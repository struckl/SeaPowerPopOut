using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>The few game objects desktop mode keeps asking for.</summary>
internal static class DesktopGame
{
    public static MainGameViewModel Vm => Globals._mainGameViewModel;

    public static bool InMission => Vm != null;

    /// <summary>The HUD is hidden (photo mode): the 3D view fills the screen.</summary>
    public static bool PhotoMode => Vm != null && !Vm.IsHudVisible;

    public static CameraManager Cameras => Singleton<CameraManager>.InstanceExists() ? Singleton<CameraManager>.Instance : null;

    public static bool? WorldViewOpen => Vm?.PipOpen;

    public static void SetWorldViewOpen(bool open)
    {
        MainGameViewModel vm = Vm;
        if (vm != null && vm.PipOpen != open)
            vm.PipOpen = open;
    }
}
