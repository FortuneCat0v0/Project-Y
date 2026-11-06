using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 桌面角色的聊天浮层：
    /// 鼠标移到角色身上出现聊天按钮，离开角色后按钮消失；
    /// 点击按钮在角色头顶打开输入框，关闭按钮收起输入框；
    /// 发送后在头顶弹出回复气泡。
    ///
    /// 界面由 <see cref="DesktopChatFactory"/> 在场景加载后生成，
    /// 按钮、输入框、气泡都按角色当前的屏幕包围盒定位，跟随窗口移动与缩放一起变化。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopChatUI : MonoBehaviour
    {
        [Header("布局")]
        [Tooltip("聊天按钮与角色右边缘的水平间距（像素）。")]
        [SerializeField] private float buttonGap = 14f;

        [Tooltip("聊天按钮相对角色中心的高度偏移（像素）。")]
        [SerializeField] private float buttonOffsetY = 24f;

        [Tooltip("输入框、气泡与角色头顶的间距（像素）。")]
        [SerializeField] private float headGap = 12f;

        [SerializeField] private float maxBubbleWidth = 260f;

        [Header("对话")]
        [SerializeField] private string replyText = "你好";
        [SerializeField] private float bubbleDuration = 4f;

        [Header("界面缩放")]
        [Tooltip("随游戏窗口缩放时，界面尺寸的取值区间，避免缩小后看不清。")]
        [SerializeField] private float minUiScale = 0.7f;
        [SerializeField] private float maxUiScale = 1.6f;

        private DesktopWindow _desktopWindow;
        private DesktopGameWindow _gameWindow;
        private Camera _targetCamera;
        private Transform _character;
        private RectTransform _canvasRect;
        private Button _chatButton;
        private RectTransform _inputPanel;
        private InputField _inputField;
        private Button _sendButton;
        private Button _closeButton;
        private RectTransform _bubble;
        private Text _bubbleText;
        private Renderer[] _characterRenderers;

        private Rect _characterRect;
        private float _rectTimer;
        private float _uiScale = 1f;
        private float _bubbleTimer;
        private bool _inputOpen;

        private static Font _uiFont;

        /// <summary>绑定场景对象并接管 <see cref="DesktopChatFactory"/> 生成的界面节点。</summary>
        public void Initialize(
            DesktopWindow desktopWindow,
            DesktopGameWindow gameWindow,
            Camera targetCamera,
            Transform character,
            RectTransform canvasRect)
        {
            _desktopWindow = desktopWindow;
            _gameWindow = gameWindow;
            _targetCamera = targetCamera;
            _character = character;
            _canvasRect = canvasRect;
            _characterRenderers = character.GetComponentsInChildren<Renderer>(true);

            _chatButton = transform.Find("ChatButton").GetComponent<Button>();
            _inputPanel = (RectTransform)transform.Find("InputPanel");
            _inputField = transform.Find("InputPanel/Input").GetComponent<InputField>();
            _sendButton = transform.Find("InputPanel/SendButton").GetComponent<Button>();
            _closeButton = transform.Find("InputPanel/CloseButton").GetComponent<Button>();
            _bubble = (RectTransform)transform.Find("Bubble");
            _bubbleText = transform.Find("Bubble/Text").GetComponent<Text>();

            EnsureEventSystem();
            ApplyFont();

            _chatButton.onClick.AddListener(OpenInput);
            _sendButton.onClick.AddListener(Submit);
            _closeButton.onClick.AddListener(CloseInput);
            _inputField.onEndEdit.AddListener(OnInputEndEdit);

            _chatButton.gameObject.SetActive(false);
            _inputPanel.gameObject.SetActive(false);
            _bubble.gameObject.SetActive(false);
        }

        private void Update()
        {
            RefreshCharacterRect();
            _uiScale = Mathf.Clamp(_gameWindow != null ? _gameWindow.Scale : 1f, minUiScale, maxUiScale);
            ApplyLayout();

            Vector2 pointer = default;
            bool hasPointer = _desktopWindow != null && _desktopWindow.TryGetPointerPosition(out pointer);
            bool overButton = hasPointer && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)_chatButton.transform, pointer, null);

            SetActive(_chatButton.gameObject, hasPointer && !_inputOpen && (_characterRect.Contains(pointer) || overButton));

            if (_bubbleTimer > 0f)
            {
                _bubbleTimer -= Time.deltaTime;
                if (_bubbleTimer <= 0f)
                {
                    _bubble.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>打开输入框并把焦点放进输入区域。</summary>
        public void OpenInput()
        {
            _inputOpen = true;
            _bubbleTimer = 0f;
            _bubble.gameObject.SetActive(false);
            _inputPanel.gameObject.SetActive(true);

            _inputField.text = string.Empty;
            _inputField.Select();
            _inputField.ActivateInputField();
        }

        /// <summary>收起输入框，按钮按下一帧的悬停状态重新显示。</summary>
        public void CloseInput()
        {
            _inputOpen = false;
            _inputPanel.gameObject.SetActive(false);
            _chatButton.gameObject.SetActive(false);
        }

        /// <summary>在头顶弹出回复气泡。</summary>
        public void ShowReply(string message)
        {
            _bubbleText.text = message;
            FitBubble();
            _bubble.gameObject.SetActive(true);
            _bubbleTimer = bubbleDuration;
        }

        private void Submit()
        {
            if (string.IsNullOrWhiteSpace(_inputField.text))
            {
                return;
            }

            CloseInput();
            ShowReply(replyText);
        }

        private void OnInputEndEdit(string _)
        {
            // 输入框已被收起时不再响应（关闭面板也会触发一次 onEndEdit）。
            if (!_inputPanel.gameObject.activeSelf)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Submit();
            }
        }

        /// <summary>气泡按文字宽度撑开，超过上限后换行。</summary>
        private void FitBubble()
        {
            _bubbleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            float naturalWidth = _bubbleText.preferredWidth;

            bool wrap = naturalWidth > maxBubbleWidth;
            _bubbleText.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;

            float width = wrap ? maxBubbleWidth : naturalWidth;
            _bubble.sizeDelta = new Vector2(width + 30f, 0f);
            _bubble.sizeDelta = new Vector2(width + 30f, _bubbleText.preferredHeight + 18f);
        }

        private void ApplyLayout()
        {
            Vector2 head = new Vector2(_characterRect.center.x, _characterRect.yMax + headGap * _uiScale);
            Vector2 button = new Vector2(
                _characterRect.xMax + buttonGap * _uiScale,
                _characterRect.center.y + buttonOffsetY * _uiScale);

            Place((RectTransform)_chatButton.transform, button);
            Place(_inputPanel, head);
            Place(_bubble, head);
        }

        private void Place(RectTransform target, Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out Vector2 local);
            target.anchoredPosition = local;
            target.localScale = Vector3.one * _uiScale;
        }

        /// <summary>角色被动画和窗口缩放影响，按固定间隔重算一次屏幕包围盒即可。</summary>
        private void RefreshCharacterRect()
        {
            _rectTimer -= Time.deltaTime;
            if (_rectTimer > 0f)
            {
                return;
            }

            _rectTimer = 0.1f;

            if (_characterRenderers == null || _targetCamera == null)
            {
                return;
            }

            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Renderer renderer in _characterRenderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (hasBounds)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            if (!hasBounds)
            {
                return;
            }

            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);

                Vector2 point = _targetCamera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            _characterRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private void ApplyFont()
        {
            Font font = GetUiFont();
            if (font == null)
            {
                return;
            }

            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }
        }

        /// <summary>系统字体带中文字形；内置字体没有中文，直接用会显示成空白。</summary>
        private static Font GetUiFont()
        {
            if (_uiFont != null)
            {
                return _uiFont;
            }

            _uiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun", "Arial" }, 16);

            if (_uiFont == null)
            {
                _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _uiFont;
        }

        /// <summary>单独打开 DesktopIdle 调试时没有 EventSystem，这里补一个；正式流程里 TEngine 的 UIRoot 已经提供。</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
