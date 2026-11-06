using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 根据鼠标下方的内容切换窗口的穿透状态：
    /// 命中 UI / 3D 碰撞体时窗口接收输入，停留在透明区域时让点击落到桌面。
    ///
    /// 因为窗口进入穿透状态后收不到鼠标移动消息，这里通过 <see cref="DesktopWindow.TryGetPointerPosition"/>
    /// 读取全局光标位置，避免穿透后“卡死”无法恢复到可交互状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopClickThroughController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private DesktopWindow desktopWindow;
        [SerializeField] private DesktopGameWindow gameWindow;
        [SerializeField] private Camera rayCamera;
        [SerializeField] private GraphicRaycaster[] graphicRaycasters;

        [Header("命中规则")]
        [Tooltip("开启后，只要射线命中任意碰撞体（例如地面）就捕获点击；关闭后只有带 DesktopInteractable 的物体捕获点击。")]
        [SerializeField] private bool captureClicksOverAnyCollider = true;
        [SerializeField] private LayerMask raycastLayers = ~0;
        [SerializeField] private float maxRayDistance = 500f;

        private readonly List<RaycastResult> _uiResults = new List<RaycastResult>();
        private PointerEventData _pointerEventData;
        private DesktopInteractable _hovered;
        private DesktopInteractable _pressed;

        private void Reset()
        {
            desktopWindow = GetComponent<DesktopWindow>();
        }

        private void Awake()
        {
            if (desktopWindow == null)
            {
                desktopWindow = GetComponent<DesktopWindow>();
            }

            if (gameWindow == null)
            {
                gameWindow = GetComponent<DesktopGameWindow>();
            }

            if (rayCamera == null)
            {
                rayCamera = Camera.main;
            }

            if (graphicRaycasters == null || graphicRaycasters.Length == 0)
            {
                graphicRaycasters = FindObjectsOfType<GraphicRaycaster>(true);
            }
        }

        private void Update()
        {
            if (desktopWindow == null)
            {
                return;
            }

            if (!desktopWindow.TryGetPointerPosition(out Vector2 pointer))
            {
                // 光标在窗口外：让窗口保持穿透，并结束悬停/按压状态。
                desktopWindow.SetClickThrough(true);
                SetHovered(null);
                CancelPressed();
                return;
            }

            bool overUI = IsPointerOverUI(pointer) || (gameWindow != null && gameWindow.ContainsPointer(pointer));
            DesktopInteractable target = null;
            bool overCollider = false;

            if (!overUI)
            {
                ResolvePointerTarget(pointer, out target, out overCollider);
            }

            bool interactive = overUI || (captureClicksOverAnyCollider ? overCollider : target != null);
            desktopWindow.SetClickThrough(!interactive);

            SetHovered(target);
            HandlePointerButtons(interactive, target);
        }

        private void OnDisable()
        {
            SetHovered(null);
            CancelPressed();

            if (desktopWindow != null)
            {
                desktopWindow.SetClickThrough(false);
            }
        }

        private void ResolvePointerTarget(Vector2 screenPosition, out DesktopInteractable target, out bool hitCollider)
        {
            target = null;
            hitCollider = false;

            if (rayCamera == null)
            {
                return;
            }

            Ray ray = rayCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            hitCollider = true;
            target = hit.collider.GetComponentInParent<DesktopInteractable>();
        }

        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (graphicRaycasters == null || graphicRaycasters.Length == 0 || EventSystem.current == null)
            {
                return false;
            }

            if (_pointerEventData == null)
            {
                _pointerEventData = new PointerEventData(EventSystem.current);
            }

            _pointerEventData.position = screenPosition;

            foreach (GraphicRaycaster raycaster in graphicRaycasters)
            {
                if (raycaster == null || !raycaster.isActiveAndEnabled)
                {
                    continue;
                }

                _uiResults.Clear();
                raycaster.Raycast(_pointerEventData, _uiResults);

                foreach (RaycastResult result in _uiResults)
                {
                    if (result.gameObject != null && result.gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void SetHovered(DesktopInteractable target)
        {
            if (_hovered == target)
            {
                return;
            }

            if (_hovered != null)
            {
                _hovered.OnHoverExit();
            }

            _hovered = target;

            if (_hovered != null)
            {
                _hovered.OnHoverEnter();
            }
        }

        private void HandlePointerButtons(bool interactive, DesktopInteractable target)
        {
            if (interactive && Input.GetMouseButtonDown(0))
            {
                _pressed = target;
                if (_pressed != null)
                {
                    _pressed.OnPointerDown();
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (_pressed != null)
                {
                    _pressed.OnPointerUp();

                    if (_pressed == target)
                    {
                        _pressed.OnClick();
                    }

                    _pressed = null;
                }
            }
            else if (_pressed != null && !Input.GetMouseButton(0))
            {
                // 鼠标抬起事件可能因为窗口在穿透状态下丢失，这里兜底清理。
                _pressed.OnPointerUp();
                _pressed = null;
            }
        }

        private void CancelPressed()
        {
            if (_pressed == null)
            {
                return;
            }

            _pressed.OnPointerUp();
            _pressed = null;
        }
    }
}
