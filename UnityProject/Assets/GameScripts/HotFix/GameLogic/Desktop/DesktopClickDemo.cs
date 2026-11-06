using UnityEngine;

namespace Game.Desktop
{
    /// <summary>
    /// 演示用交互物：悬停变色、点击压缩回弹并旋转。
    /// 它只负责表现，命中与点击派发由 <see cref="DesktopClickThroughController"/> 完成。
    /// </summary>
    [RequireComponent(typeof(DesktopInteractable))]
    public sealed class DesktopClickDemo : MonoBehaviour
    {
        [Header("颜色")]
        [SerializeField] private Color baseColor = new Color(1f, 0.55f, 0.18f, 1f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.82f, 0.32f, 1f);
        [SerializeField] private float colorLerpSpeed = 14f;

        [Header("点击反馈")]
        [SerializeField] private float clickSquash = 0.35f;
        [SerializeField] private float squashRecoverSpeed = 12f;
        [SerializeField] private float clickSpinDegrees = 180f;

        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private DesktopInteractable _interactable;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;
        private Color _currentColor;
        private Vector3 _baseScale;
        private float _squash;
        private float _spinRemaining;

        private void Awake()
        {
            _interactable = GetComponent<DesktopInteractable>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _propertyBlock = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            _currentColor = baseColor;
            ApplyColor(_currentColor);
        }

        private void OnEnable()
        {
            if (_interactable != null)
            {
                _interactable.Clicked += HandleClicked;
            }
        }

        private void OnDisable()
        {
            if (_interactable != null)
            {
                _interactable.Clicked -= HandleClicked;
            }
        }

        private void Update()
        {
            Color target = _interactable != null && _interactable.IsHovered ? hoverColor : baseColor;
            float colorT = 1f - Mathf.Exp(-colorLerpSpeed * Time.deltaTime);
            _currentColor = Color.Lerp(_currentColor, target, colorT);
            ApplyColor(_currentColor);

            float squashT = 1f - Mathf.Exp(-squashRecoverSpeed * Time.deltaTime);
            _squash = Mathf.Lerp(_squash, 0f, squashT);

            float vertical = 1f - _squash;
            float horizontal = 1f + _squash * 0.5f;
            transform.localScale = new Vector3(
                _baseScale.x * horizontal,
                _baseScale.y * vertical,
                _baseScale.z * horizontal);

            if (_spinRemaining > 0.01f)
            {
                float step = Mathf.Min(_spinRemaining, 360f * Time.deltaTime);
                transform.Rotate(Vector3.up, step, Space.Self);
                _spinRemaining -= step;
            }
        }

        private void HandleClicked(DesktopInteractable _)
        {
            _squash = clickSquash;
            _spinRemaining += clickSpinDegrees;
        }

        private void ApplyColor(Color color)
        {
            if (_renderers == null)
            {
                return;
            }

            _propertyBlock.SetColor(ColorPropertyId, color);
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(_propertyBlock);
                }
            }
        }
    }
}
