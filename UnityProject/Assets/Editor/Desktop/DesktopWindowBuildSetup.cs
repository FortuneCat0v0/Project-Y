#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Desktop.EditorTools
{
    /// <summary>
    /// Windows 透明窗口的构建前配置。
    /// DWM 逐像素透明依赖 BitBlt 交换链，DXGI Flip Model 下透明会失效，
    /// 因此 Windows Player 构建前自动关闭 Player Settings 的 Use DXGI Flip Model Swapchain。
    /// </summary>
    public sealed class DesktopWindowBuildSetup : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows &&
                report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            EnsureTransparentWindowSettings();
        }

        [MenuItem("Tools/Desktop Window/Configure Transparent Window Player Settings")]
        private static void ConfigureFromMenu()
        {
            EnsureTransparentWindowSettings();
            Debug.Log("[DesktopWindow] Player Settings 已适配透明窗口。");
        }

        private static void EnsureTransparentWindowSettings()
        {
            if (PlayerSettings.useFlipModelSwapchain)
            {
                PlayerSettings.useFlipModelSwapchain = false;
                Debug.LogWarning(
                    "[DesktopWindow] 已关闭 Use DXGI Flip Model Swapchain。" +
                    "DWM 透明窗口只支持 BitBlt 交换链，启用 Flip Model 时透明区域会变黑。");
            }

            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            if (apis.Length > 0 && apis[0] == GraphicsDeviceType.Direct3D12)
            {
                Debug.LogWarning(
                    "[DesktopWindow] 当前 Windows 构建首选 D3D12。D3D12 始终使用 Flip Model，" +
                    "桌面透明不可用；请把 Direct3D11 移到图形 API 列表首位。");
            }
        }
    }
}
#endif
