using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TiltDrop.Game
{
    /// <summary>uGUI built from code (no prefabs/scenes needed). Reference resolution 1080x1920 portrait.</summary>
    public static class UIKit
    {
        public static Font Font;
        public static Action OnAnyClick;

        public static Font LoadFont()
        {
            if (Font != null) return Font;
            try { Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
            if (Font == null) { try { Font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { } }
            return Font;
        }

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                UnityEngine.Object.DontDestroyOnLoad(es);
            }
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor at a normalized point with a fixed size.</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float w, float h, float x = 0, float y = 0)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        /// <summary>Full-width band anchored to top (ay=1) or bottom (ay=0).</summary>
        public static RectTransform Band(this RectTransform rt, float ay, float height, float y = 0, float side = 0)
        {
            rt.anchorMin = new Vector2(0, ay);
            rt.anchorMax = new Vector2(1, ay);
            rt.pivot = new Vector2(0.5f, ay);
            rt.sizeDelta = new Vector2(-2 * side, height);
            rt.anchoredPosition = new Vector2(0, y);
            return rt;
        }

        public static Image Panel(Transform parent, Color color, string name = "panel")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Sprites.Panel;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Image Icon(Transform parent, Sprite sprite, Color color, float size)
        {
            var rt = Rect("icon", parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false)
        {
            var rt = Rect("label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = LoadFont();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Button Button(Transform parent, string text, Color color, Action onClick, int fontSize = 46, float height = 132)
        {
            var img = Panel(parent, color, "button");
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => { OnAnyClick?.Invoke(); onClick(); });
            img.gameObject.AddComponent<PressFeel>();
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            if (!string.IsNullOrEmpty(text))
            {
                var label = Label(img.transform, text, fontSize, Color.white, TextAnchor.MiddleCenter, true);
                label.rectTransform.Stretch(16, 4, 16, 4);
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true; // long labels (store rows) shrink instead of overflowing
                label.resizeTextMinSize = 22;
                label.resizeTextMaxSize = fontSize;
            }
            return btn;
        }

        public static void SetText(this Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int pad = 0, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, int pad = 0, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Size(this Component c, float height = -1, float width = -1, float flexW = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        /// <summary>Row of up to 3 star images (filled = gold).</summary>
        public static RectTransform StarRow(Transform parent, int stars, float size, float spacing = 6)
        {
            var row = Rect("stars", parent);
            var h = HStack(row, spacing);
            h.childForceExpandWidth = false;
            h.childControlWidth = false;
            h.childControlHeight = false;
            for (int i = 0; i < 3; i++) Icon(row, Sprites.Star, i < stars ? Palette.Gold : new Color(1, 1, 1, 0.15f), size);
            row.Size(size);
            return row;
        }

        public static void FadeIn(CanvasGroup g, float dur = 0.18f)
        {
            g.alpha = 0f;
            var t = g.transform;
            Tweener.Run(dur, p => { if (g == null) return; g.alpha = p; t.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Ease.OutBack(p)); }, Ease.OutCubic, 0f, null, g);
        }
    }

    /// <summary>Press-scale feedback for buttons (the small things make it feel good).</summary>
    public sealed class PressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e) => Animate(0.94f);
        public void OnPointerUp(PointerEventData e) => Animate(1f);

        void Animate(float target)
        {
            Tweener.Kill(this);
            var t = transform;
            float from = t.localScale.x;
            Tweener.Run(0.08f, p => { if (t != null) t.localScale = Vector3.one * Mathf.Lerp(from, target, p); }, Ease.OutQuad, 0f, null, this);
        }
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notches, home indicator).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last;

        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Modal popup with dimmed background. Content goes into Body (vertical stack).</summary>
    public sealed class Popup
    {
        public readonly RectTransform Root, Body;
        public Action OnClosed;

        public Popup(Transform parent, string title, float width = 900)
        {
            Root = UIKit.Rect("popup", parent).Stretch();
            var dim = Root.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.62f);
            var group = Root.gameObject.AddComponent<CanvasGroup>();
            var panel = UIKit.Panel(Root, Palette.Panel, "card");
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(width, 0);
            Body = prt;
            UIKit.VStack(prt, 26, 48);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (!string.IsNullOrEmpty(title)) UIKit.Label(prt, title, 64, Palette.Text, TextAnchor.MiddleCenter, true).Size(90);
            UIKit.FadeIn(group);
        }

        /// <summary>Wrapped paragraph; its height comes from the text's preferred height in the vertical layout.</summary>
        public Text Text(string text, int size = 44, Color? color = null) => UIKit.Label(Body, text, size, color ?? Palette.TextDim);

        public Button Button(string text, Color color, Action onClick, bool closes = true) =>
            UIKit.Button(Body, text, color, () => { if (closes) Close(); onClick?.Invoke(); });

        public void Close()
        {
            if (Root == null) return;
            UnityEngine.Object.Destroy(Root.gameObject);
            OnClosed?.Invoke();
        }
    }
}
