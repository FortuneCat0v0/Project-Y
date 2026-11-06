using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 场景加载完成后用代码搭出角色聊天浮层（按钮 / 输入框 / 回复气泡）。
    /// 由 <see cref="GameApp"/> 在 DesktopIdle 加载完成后调用，不依赖编辑器预制数据。
    ///
    /// 圆角、圆形、三角尾巴三张切图在运行时生成，聊天图标取 AssetRaw 里的 chat.png（YooAsset 地址 chat）。
    /// </summary>
    public static class DesktopChatFactory
    {
        private static readonly Color PanelColor = new Color(0.16f, 0.16f, 0.16f, 0.95f);
        private static readonly Color BorderColor = new Color(0.73f, 0.82f, 0.96f, 1f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color PlaceholderColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        private static readonly Color SendColor = new Color(0.96f, 0.97f, 1f, 1f);
        private static readonly Color SendTextColor = new Color(0.17f, 0.17f, 0.17f, 1f);

        public static void Create()
        {
            GameObject gameWindow = GameObject.Find("GameWindow");
            GameObject desktopWindow = GameObject.Find("DesktopWindow");
            GameObject character = GameObject.Find("GameRoot/Haruka");
            if (gameWindow == null || desktopWindow == null || character == null)
            {
                Log.Warning("[DesktopChat] 场景里找不到 GameWindow / DesktopWindow / GameRoot/Haruka，聊天浮层未创建。");
                return;
            }

            RectTransform canvasRect = (RectTransform)gameWindow.transform;

            Sprite round = CreateRoundedSprite("ChatRound", 64, 16f, 1f, new Vector4(16f, 16f, 16f, 16f));
            Sprite circle = CreateRoundedSprite("ChatCircle", 64, 32f, 1.4f, Vector4.zero);
            Sprite tail = CreateTriangleSprite("ChatTail", 32, 18);
            Sprite icon = GameModule.Resource.LoadAsset<Sprite>("chat");

            RectTransform root = CreateRect("ChatUI", canvasRect);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);

            BuildChatButton(root, circle, icon);
            BuildInputPanel(root, round, circle);
            BuildBubble(root, round, tail);

            DesktopChatUI chatUI = root.gameObject.AddComponent<DesktopChatUI>();
            chatUI.Initialize(
                desktopWindow.GetComponent<DesktopWindow>(),
                desktopWindow.GetComponent<DesktopGameWindow>(),
                Camera.main,
                character.transform,
                canvasRect);
        }

        private static void BuildChatButton(RectTransform root, Sprite circle, Sprite icon)
        {
            RectTransform rect = CreateRect("ChatButton", root);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = circle;
            image.color = PanelColor;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(46f, 46f);

            RectTransform iconRect = CreateRect("Icon", rect);
            Image iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(24f, 25f);
        }

        private static void BuildInputPanel(RectTransform root, Sprite round, Sprite circle)
        {
            RectTransform panel = CreateRect("InputPanel", root);
            Image image = panel.gameObject.AddComponent<Image>();
            image.sprite = round;
            image.type = Image.Type.Sliced;
            image.color = PanelColor;

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(2f, -2f);

            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(300f, 58f);

            RectTransform inputRect = CreateRect("Input", panel);
            Image inputBackground = inputRect.gameObject.AddComponent<Image>();
            inputBackground.color = new Color(1f, 1f, 1f, 0f);
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = new Vector2(16f, 9f);
            inputRect.offsetMax = new Vector2(-76f, -9f);

            Text text = CreateText("Text", inputRect, string.Empty, 15, TextColor, TextAnchor.MiddleLeft);
            StretchFull(text.rectTransform);

            Text placeholder = CreateText("Placeholder", inputRect, "输入想说的话…", 15, PlaceholderColor, TextAnchor.MiddleLeft);
            StretchFull(placeholder.rectTransform);

            InputField inputField = inputRect.gameObject.AddComponent<InputField>();
            inputField.targetGraphic = inputBackground;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = 40;
            inputField.caretWidth = 2;
            inputField.caretColor = Color.white;
            inputField.selectionColor = new Color(0.55f, 0.72f, 1f, 0.45f);

            RectTransform sendRect = CreateRect("SendButton", panel);
            Image sendImage = sendRect.gameObject.AddComponent<Image>();
            sendImage.sprite = round;
            sendImage.type = Image.Type.Sliced;
            sendImage.color = SendColor;

            Button sendButton = sendRect.gameObject.AddComponent<Button>();
            sendButton.targetGraphic = sendImage;

            sendRect.anchorMin = new Vector2(1f, 0.5f);
            sendRect.anchorMax = new Vector2(1f, 0.5f);
            sendRect.pivot = new Vector2(1f, 0.5f);
            sendRect.anchoredPosition = new Vector2(-11f, 0f);
            sendRect.sizeDelta = new Vector2(58f, 36f);

            Text sendLabel = CreateText("Label", sendRect, "发送", 14, SendTextColor, TextAnchor.MiddleCenter);
            StretchFull(sendLabel.rectTransform);

            RectTransform closeRect = CreateRect("CloseButton", panel);
            Image closeImage = closeRect.gameObject.AddComponent<Image>();
            closeImage.sprite = circle;
            closeImage.color = new Color(0.16f, 0.16f, 0.16f, 0.9f);

            Button closeButton = closeRect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;

            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(10f, 10f);
            closeRect.sizeDelta = new Vector2(24f, 24f);

            Text closeLabel = CreateText("Label", closeRect, "×", 16, TextColor, TextAnchor.MiddleCenter);
            StretchFull(closeLabel.rectTransform);
        }

        private static void BuildBubble(RectTransform root, Sprite round, Sprite tail)
        {
            RectTransform bubble = CreateRect("Bubble", root);
            Image image = bubble.gameObject.AddComponent<Image>();
            image.sprite = round;
            image.type = Image.Type.Sliced;
            image.color = PanelColor;
            image.raycastTarget = false;

            bubble.anchorMin = new Vector2(0.5f, 0.5f);
            bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = new Vector2(0.5f, 0f);
            bubble.sizeDelta = new Vector2(96f, 54f);

            RectTransform tailRect = CreateRect("Tail", bubble);
            Image tailImage = tailRect.gameObject.AddComponent<Image>();
            tailImage.sprite = tail;
            tailImage.color = PanelColor;
            tailImage.raycastTarget = false;
            tailRect.anchorMin = new Vector2(0.5f, 0f);
            tailRect.anchorMax = new Vector2(0.5f, 0f);
            tailRect.pivot = new Vector2(0.5f, 1f);
            tailRect.anchoredPosition = Vector2.zero;
            tailRect.sizeDelta = new Vector2(18f, 11f);

            Text bubbleText = CreateText("Text", bubble, "你好", 16, TextColor, TextAnchor.MiddleCenter);
            bubbleText.rectTransform.anchorMin = Vector2.zero;
            bubbleText.rectTransform.anchorMax = Vector2.one;
            bubbleText.rectTransform.offsetMin = new Vector2(15f, 9f);
            bubbleText.rectTransform.offsetMax = new Vector2(-15f, -9f);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            return (RectTransform)target.transform;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor anchor)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite CreateRoundedSprite(string name, int size, float radius, float softness, Vector4 border)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedAlpha(x + 0.5f, y + 0.5f, size, size, radius, softness);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateTriangleSprite(string name, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = 1f - y / (float)height;
                float halfWidth = 0.5f * (1f - v);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float alpha = Mathf.Clamp01((halfWidth - Mathf.Abs(u - 0.5f)) * width * 0.9f);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>圆角矩形的距离场，1 像素抗锯齿；<paramref name="softness"/> 用来给小尺寸贴图更柔的边缘。</summary>
        private static float RoundedAlpha(float x, float y, float width, float height, float radius, float softness)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float dx = Mathf.Abs(x - halfWidth) - (halfWidth - radius);
            float dy = Mathf.Abs(y - halfHeight) - (halfHeight - radius);
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return Mathf.Clamp01((softness * 0.5f - (outside + inside - radius)) / softness);
        }
    }
}
