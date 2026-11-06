using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 把桌面上的游戏画面当成一块可移动、可缩放的“虚拟窗口”。
    /// 原生窗口保持无边框全屏透明，移动和缩放的是场景内容，
    /// 因此点击穿透、透明背景都不受影响（参考游戏 Ropuka's Idle Island 也是同样做法）。
    ///
    /// 拖动左上角手柄移动游戏，拖动右下角手柄缩放游戏；
    /// 手柄是屏幕空间 UI，每帧跟随内容投影到屏幕后的包围盒。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(DesktopWindow))]
    [DisallowMultipleComponent]
    public sealed class DesktopGameWindow : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private RectTransform handleCanvas;
        [SerializeField] private RectTransform moveHandle;
        [SerializeField] private RectTransform resizeHandle;

        [Header("缩放范围")]
        [SerializeField] private float minScale = 0.3f;
        [SerializeField] private float maxScale = 3f;

        [Header("手柄颜色")]
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color activeColor = new Color(1f, 1f, 1f, 0.9f);

        private DesktopWindow _desktopWindow;
        private Image _moveImage;
        private Image _resizeImage;

        // 内容在初始布局下的位置和屏幕尺寸，移动与缩放都以它为基准。
        private Vector3 _baseCenterLocal;
        private Vector2 _baseSize;
        private float _baseDepth;

        private Vector2 _topLeft;
        private float _scale = 1f;

        private Vector2 _lastPointer;
        private bool _moving;
        private bool _resizing;

        /// <summary>是否正在拖动或缩放；期间窗口必须保持接收输入。</summary>
        public bool IsInteracting => _moving || _resizing;

        /// <summary>当前内容的缩放比例，界面元素按它等比缩放。</summary>
        public float Scale => _scale;

        private void Awake()
        {
            _desktopWindow = GetComponent<DesktopWindow>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            _moveImage = moveHandle.GetComponent<Image>();
            _resizeImage = resizeHandle.GetComponent<Image>();
        }

        private void Start()
        {
            Bounds bounds = CalculateBounds(contentRoot);
            _baseCenterLocal = contentRoot.InverseTransformPoint(bounds.center);
            _baseDepth = Vector3.Dot(bounds.center - targetCamera.transform.position, targetCamera.transform.forward);

            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector2 point = targetCamera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            _baseSize = max - min;
            _topLeft = new Vector2(min.x, max.y);
        }

        private void Update()
        {
            ApplyLayout();
            HandleInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 拖动中切走会丢失 MouseUp，这里主动收尾，避免回来后继续跟随鼠标。
            if (!hasFocus)
            {
                _moving = false;
                _resizing = false;
            }
        }

        /// <summary>指针是否落在手柄上（拖动中始终为 true），用于关闭窗口穿透。</summary>
        public bool ContainsPointer(Vector2 screenPoint)
        {
            return IsInteracting
                || RectTransformUtility.RectangleContainsScreenPoint(moveHandle, screenPoint, null)
                || RectTransformUtility.RectangleContainsScreenPoint(resizeHandle, screenPoint, null);
        }

        /// <summary>按屏幕像素移动游戏位置。</summary>
        public void MoveBy(Vector2 deltaPixels)
        {
            _topLeft += deltaPixels;
        }

        /// <summary>按屏幕像素缩放游戏，左上角固定，右下角跟随拖动。</summary>
        public void ResizeBy(Vector2 deltaPixels)
        {
            float delta = (deltaPixels.x / _baseSize.x - deltaPixels.y / _baseSize.y) * 0.5f;
            _scale = Mathf.Clamp(_scale + delta, minScale, maxScale);
        }

        private void ApplyLayout()
        {
            Vector2 size = _baseSize * _scale;
            Vector2 center = new Vector2(_topLeft.x + size.x * 0.5f, _topLeft.y - size.y * 0.5f);

            contentRoot.localScale = Vector3.one * _scale;
            contentRoot.position = ViewPlanePoint(center, _baseDepth)
                - contentRoot.rotation * (_baseCenterLocal * _scale);

            PlaceHandle(moveHandle, new Vector2(_topLeft.x, _topLeft.y));
            PlaceHandle(resizeHandle, new Vector2(_topLeft.x + size.x, _topLeft.y - size.y));
        }

        private void HandleInput()
        {
            if (!_desktopWindow.TryGetPointerPosition(out Vector2 pointer))
            {
                _moving = false;
                _resizing = false;
                return;
            }

            bool overMove = RectTransformUtility.RectangleContainsScreenPoint(moveHandle, pointer, null);
            bool overResize = RectTransformUtility.RectangleContainsScreenPoint(resizeHandle, pointer, null);

            if (Input.GetMouseButtonDown(0))
            {
                _moving = overMove;
                _resizing = !overMove && overResize;
                _lastPointer = pointer;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _moving = false;
                _resizing = false;
            }
            else if (IsInteracting)
            {
                Vector2 delta = pointer - _lastPointer;
                _lastPointer = pointer;

                if (_moving)
                {
                    MoveBy(delta);
                }
                else
                {
                    ResizeBy(delta);
                }
            }

            _moveImage.color = _moving || overMove ? activeColor : idleColor;
            _resizeImage.color = _resizing || overResize ? activeColor : idleColor;
        }

        private void PlaceHandle(RectTransform handle, Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handleCanvas, screenPoint, null, out Vector2 local);
            handle.anchoredPosition = local;
        }

        /// <summary>求出屏幕点在指定相机深度处对应的世界坐标，用于把内容摆到画面上的目标位置。</summary>
        private Vector3 ViewPlanePoint(Vector2 screenPoint, float depth)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            float distance = depth / Vector3.Dot(ray.direction, targetCamera.transform.forward);
            return ray.origin + ray.direction * distance;
        }

        private static Bounds CalculateBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
