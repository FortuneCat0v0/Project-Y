# 桌面放置基础（DesktopIdle 场景）

场景：`Assets/AssetRaw/Scenes/DesktopIdle.unity`。
它是 YooAsset 收集的场景（收集规则 `AddressByFileName`，定位地址为 `DesktopIdle`），
由 [GameApp.cs](../GameApp.cs) 在启动流程末尾通过 `GameModule.Scene.LoadSceneAsync("DesktopIdle")` 切为主场景。
因此不要把它加入 Build Settings，避免和 YooAsset 包内的场景重复。

本目录属于热更程序集 `GameLogic`（`Assets/GameScripts/HotFix/GameLogic/GameLogic.asmdef`），
所以这些脚本会随热更 DLL 一起发布；只有 `Assets/Editor/Desktop/DesktopWindowBuildSetup.cs` 留在编辑器程序集，
负责构建前关闭 Flip Model。场景挂载的热更组件要求先加载热更程序集、再加载 `DesktopIdle`，
当前 `ProcedureLoadAssembly` 的执行顺序满足这一点。

## 场景组成

| 对象 | 作用 |
|---|---|
| `DesktopWindow` | 窗口透明、置顶、点击穿透与全局光标读取 |
| `DesktopClickThroughController` | 根据鼠标下的 UI / 碰撞体决定是否穿透，并派发悬停、点击 |
| `DesktopGameWindow` | 移动 / 缩放游戏内容，维护两个屏幕空间手柄 |
| `GameRoot` | 游戏内容根节点，`Ground` 与 `DemoCube` 的父级，整体位移和缩放 |
| `GameWindow` | 屏幕空间 Overlay 画布，挂着 `MoveHandle`、`ResizeHandle` 两个手柄 |
| `Main Camera` | 透视相机，Clear Flags = Solid Color，背景 alpha = 0 |
| `Ground` | 悬浮平台（Cube），作为场景地面和可见的点击捕获区域 |
| `DemoCube` | 演示用可交互物，带 `DesktopInteractable` + `DesktopClickDemo` |
| `Directional Light` | 主光与软阴影 |

## 移动与缩放

参考游戏的做法是窗口保持全屏透明，移动和缩放的是画面内容，而不是原生窗口。
本项目沿用同一思路：

- 左上角手柄拖动：`GameRoot` 整体平移，画面跟着鼠标走。
- 右下角手柄拖动：`GameRoot` 等比缩放，左上角固定，右下角跟随拖动。
- 手柄是 `Screen Space - Overlay` 画布上的 UI，每帧根据 `GameRoot` 内容的屏幕包围盒摆到左上 / 右下角，
  指针落在手柄上时 `DesktopClickThroughController` 会关闭窗口穿透，所以穿透区域仍能正常操作手柄。

内容的位置由“屏幕像素”换算到相机视线平面，因此缩放后画面仍贴合手柄框。
`DesktopGameWindow.minScale / maxScale` 控制缩放范围（0.3 ~ 3 倍）。

## 运行方式

- **编辑器**：只能预览构图。透明窗口的原生调用在 `Application.isEditor` 时跳过，透明区域会显示为黑色。
  但 TEngine 的启动流程和场景切换可以在编辑器 Play 模式下完整验证。
- **Windows Player**：透明、置顶、穿透才会生效。默认是无边框全屏（`WindowMode.BorderlessFullscreen`），可在 `DesktopWindow` 组件上改成 `Windowed` 并指定窗口尺寸。
- 构建后从 `main` 场景启动，热更流程执行到 `GameApp` 时会自动切到 `DesktopIdle`。

### 必须的 Player Settings

DWM 逐像素透明只支持 BitBlt 交换链，因此需要关闭：

`Project Settings > Player > Resolution and Presentation > Use DXGI Flip Model Swapchain`

`Assets/Editor/Desktop/DesktopWindowBuildSetup.cs` 会在 Windows Player 构建前自动关闭该选项，并提示首选图形 API 需要是 Direct3D11（D3D12 始终使用 Flip Model，无法透明）。
也可以手动执行菜单：`Tools/Desktop Window/Configure Transparent Window Player Settings`。

## 点击穿透规则

- 每帧用 `GetCursorPos + ScreenToClient` 读取全局光标（窗口穿透时收不到鼠标移动消息，不能依赖 `Input.mousePosition`）。
- 光标命中 UI（`GraphicRaycaster`）或 3D 碰撞体时，移除 `WS_EX_TRANSPARENT`，窗口接收输入。
- 光标在完全透明的区域时，加上 `WS_EX_TRANSPARENT`，点击落到桌面/下层窗口。
- `captureClicksOverAnyCollider = true`：地面这类可见碰撞体也会捕获点击；
  设为 `false` 时只有带 `DesktopInteractable` 的物体捕获点击，其余区域全部穿透。

## 扩展

- 新的可点击物：挂 `DesktopInteractable`（或继承并重写 `OnClick` / `OnHoverEnter` 等），并确保有 Collider。
- UI 交互：把 Canvas 的 `GraphicRaycaster` 拖到控制器的 `graphicRaycasters`（留空时自动查找场景中的全部 Raycaster）。
- 只让指定层参与命中：调整 `raycastLayers`。
- 多显示器切换、位置持久化尚未实现。
