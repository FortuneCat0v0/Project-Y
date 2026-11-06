using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Desktop
{
    /// <summary>
    /// 桌面放置游戏的窗口基础能力：透明背景、置顶、点击穿透。
    /// 只在 Windows Standalone Player 中操作原生窗口；编辑器里仅把相机清屏色设为透明色以便预览构图。
    ///
    /// 透明原理：DwmExtendFrameIntoClientArea + WS_EX_LAYERED + LWA_ALPHA，
    /// 相机的 Clear Flags 必须是 Solid Color，且背景色 alpha 为 0。
    /// 该方案依赖 BitBlt 交换链，Player Settings 里需要关闭 “Use DXGI Flip Model Swapchain”；
    /// 项目内的构建前处理与菜单会负责检查并关闭该选项。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopWindow : MonoBehaviour
    {
        public enum WindowMode
        {
            /// <summary>无边框全屏，作为覆盖整个桌面的透明层。</summary>
            BorderlessFullscreen = 0,

            /// <summary>普通窗口，便于开发调试或做成悬浮小窗。</summary>
            Windowed = 1,
        }

        [Header("窗口")]
        [SerializeField] private WindowMode windowMode = WindowMode.BorderlessFullscreen;
        [SerializeField] private Vector2Int windowedSize = new Vector2Int(1280, 720);
        [SerializeField] private bool alwaysOnTop = true;
        [SerializeField] private bool hideFromTaskbar = false;
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool runInBackground = true;

        [Header("透明")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Color transparentColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>当前是否处于鼠标穿透状态（点击会落到桌面/下层窗口）。</summary>
        public bool IsClickThrough { get; private set; }

        /// <summary>原生窗口能力是否可用；编辑器内恒为 false。</summary>
        public bool IsNativeWindowAvailable =>
            !Application.isEditor && Application.platform == RuntimePlatform.WindowsPlayer;

        private IntPtr _windowHandle = IntPtr.Zero;
        private bool _transparencyApplied;
        private int _clickThroughState = -1;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            ApplyCameraSettings();

            Application.runInBackground = runInBackground;
            Application.targetFrameRate = targetFrameRate;

            if (!IsNativeWindowAvailable)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            ApplyWindowMode();

            _windowHandle = GetActiveWindow();
            if (_windowHandle == IntPtr.Zero)
            {
                Debug.LogError("[DesktopWindow] 无法获取窗口句柄，透明窗口未启用。");
                return;
            }

            ApplyTransparency();
            ApplyAlwaysOnTop();
            ApplyTaskbarVisibility();
            SetClickThrough(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 全屏切换、分辨率变化或丢失焦点后，DWM/扩展样式可能被重置，这里重新应用一次。
            if (hasFocus && IsNativeWindowAvailable && _transparencyApplied)
            {
                RefreshWindow();
            }
        }

        /// <summary>重新应用透明、置顶与帧率设置（不重置窗口分辨率）。</summary>
        public void RefreshWindow()
        {
            if (!IsNativeWindowAvailable)
            {
                return;
            }

            if (_windowHandle == IntPtr.Zero)
            {
                _windowHandle = GetActiveWindow();
            }

            ApplyTransparency();
            ApplyAlwaysOnTop();
            ApplyTaskbarVisibility();
            Application.targetFrameRate = targetFrameRate;
        }

        /// <summary>
        /// 切换鼠标穿透。只在状态变化时调用 Win32，可每帧安全调用。
        /// </summary>
        public void SetClickThrough(bool clickThrough)
        {
            IsClickThrough = clickThrough;

            if (!IsNativeWindowAvailable || _windowHandle == IntPtr.Zero)
            {
                return;
            }

            int desired = clickThrough ? 1 : 0;
            if (_clickThroughState == desired)
            {
                return;
            }

            long style = GetWindowLong(_windowHandle, GWL_EXSTYLE);
            if (clickThrough)
            {
                style |= WS_EX_TRANSPARENT;
            }
            else
            {
                style &= ~WS_EX_TRANSPARENT;
            }

            SetWindowLong(_windowHandle, GWL_EXSTYLE, (uint)style);
            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            _clickThroughState = desired;
        }

        /// <summary>
        /// 取得鼠标在游戏画面中的像素坐标。穿透状态下窗口收不到鼠标消息，
        /// 因此统一用 GetCursorPos + ScreenToClient 读取全局光标位置。
        /// </summary>
        public bool TryGetPointerPosition(out Vector2 screenPosition)
        {
            if (IsNativeWindowAvailable && _windowHandle != IntPtr.Zero)
            {
                if (GetClientRect(_windowHandle, out Rect clientRect) &&
                    GetCursorPos(out Point point) &&
                    ScreenToClient(_windowHandle, ref point))
                {
                    int width = Mathf.Max(1, clientRect.Right - clientRect.Left);
                    int height = Mathf.Max(1, clientRect.Bottom - clientRect.Top);

                    // 高 DPI 下客户区像素和渲染分辨率可能不一致，按比例换算到游戏画面坐标。
                    float scaleX = Screen.width / (float)width;
                    float scaleY = Screen.height / (float)height;
                    float x = (point.X - clientRect.Left) * scaleX;
                    float y = (clientRect.Bottom - point.Y) * scaleY;

                    if (x >= 0f && y >= 0f && x <= Screen.width && y <= Screen.height)
                    {
                        screenPosition = new Vector2(x, y);
                        return true;
                    }
                }

                screenPosition = default;
                return false;
            }

            // 编辑器/其他平台：使用 UnityEngine 的鼠标位置，至少能在 Game 视图里调试交互。
            screenPosition = Input.mousePosition;
            return true;
        }

        private void ApplyCameraSettings()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                Debug.LogWarning("[DesktopWindow] 没有找到相机，透明背景不会被设置。");
                return;
            }

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = transparentColor;
            targetCamera.allowHDR = false;
            targetCamera.allowMSAA = false;
            targetCamera.targetTexture = null;
        }

        private void ApplyWindowMode()
        {
            if (windowMode == WindowMode.BorderlessFullscreen)
            {
                Resolution resolution = Screen.currentResolution;
                Screen.SetResolution(
                    resolution.width,
                    resolution.height,
                    FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(
                    Mathf.Max(320, windowedSize.x),
                    Mathf.Max(180, windowedSize.y),
                    FullScreenMode.Windowed);
            }
        }

        private void ApplyTransparency()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            Margins margins = new Margins
            {
                LeftWidth = -1,
                RightWidth = -1,
                TopHeight = -1,
                BottomHeight = -1,
            };
            DwmExtendFrameIntoClientArea(_windowHandle, ref margins);

            long style = GetWindowLong(_windowHandle, GWL_EXSTYLE);
            style |= WS_EX_LAYERED;
            SetWindowLong(_windowHandle, GWL_EXSTYLE, (uint)style);
            SetLayeredWindowAttributes(_windowHandle, 0, 255, LWA_ALPHA);

            SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            RedrawWindow(
                _windowHandle,
                IntPtr.Zero,
                IntPtr.Zero,
                RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME);

            _transparencyApplied = true;

            // 样式被重写后强制下一次 SetClickThrough 重新落盘。
            _clickThroughState = -1;
        }

        private void ApplyAlwaysOnTop()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                _windowHandle,
                alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void ApplyTaskbarVisibility()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            long style = GetWindowLong(_windowHandle, GWL_EXSTYLE);
            if (hideFromTaskbar)
            {
                style |= WS_EX_TOOLWINDOW;
            }
            else
            {
                style &= ~WS_EX_TOOLWINDOW;
            }

            SetWindowLong(_windowHandle, GWL_EXSTYLE, (uint)style);
        }

        private const int GWL_EXSTYLE = -20;

        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;

        private const uint LWA_ALPHA = 0x00000002;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_FRAME = 0x0400;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int LeftWidth;
            public int RightWidth;
            public int TopHeight;
            public int BottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(
            IntPtr hWnd,
            uint crKey,
            byte bAlpha,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(
            IntPtr hWnd,
            IntPtr lprcUpdate,
            IntPtr hrgnUpdate,
            uint flags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref Point point);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);
    }
}
