using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Desktop
{
    /// <summary>
    /// 桌面窗口中的可交互物体标记。由 <see cref="DesktopClickThroughController"/> 负责命中、
    /// 悬停和点击派发；穿透判断时也会识别它。
    /// </summary>
    public class DesktopInteractable : MonoBehaviour
    {
        [SerializeField]
        private UnityEvent onClick = new UnityEvent();

        /// <summary>点击回调，参数为被点击的物体。</summary>
        public event Action<DesktopInteractable> Clicked;

        public bool IsHovered { get; private set; }

        public virtual void OnHoverEnter()
        {
            IsHovered = true;
        }

        public virtual void OnHoverExit()
        {
            IsHovered = false;
        }

        public virtual void OnPointerDown()
        {
        }

        public virtual void OnPointerUp()
        {
        }

        public virtual void OnClick()
        {
            onClick?.Invoke();
            Clicked?.Invoke(this);
        }
    }
}
