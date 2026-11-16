using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBoxReborn.UI;

internal static class Theme
{
    public static readonly Color Backdrop = Hex("0B0C0FF0");
    public static readonly Color Border = Hex("2A2E37");
    public static readonly Color Surface = Hex("15171CF5");
    public static readonly Color SurfaceHi = Hex("1D2027");
    public static readonly Color Track = Hex("262A32");
    public static readonly Color Text = Hex("ECEDF1");
    public static readonly Color Muted = Hex("8D929E");
    public static readonly Color Faint = Hex("5B606B");

    // Naranja de caja negra de avión.
    public static readonly Color Accent = Hex("FF7A1A");
    public static readonly Color Good = Hex("6CCB5F");
    public static readonly Color Bad = Hex("FF4D4F");
    public static readonly Color Warn = Hex("F5B841");
    public static readonly Color Info = Hex("5AA9FF");
    public static readonly Color Remaining = Hex("4A505C");

    private static readonly Color[] PlayerColors =
    {
        Hex("5AA9FF"), Hex("F5B841"), Hex("B07CFF"), Hex("4CD3C2"),
        Hex("FF6FA8"), Hex("9BD45A"), Hex("FF8A5A"), Hex("7F8CFF"),
    };

    public static Color Player(int order, bool unknown) =>
        unknown ? Muted : PlayerColors[Mathf.Abs(order) % PlayerColors.Length];

    public static string Rich(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}

/// <summary>Piezas de uGUI construidas por código: el mod no lleva asset bundles.</summary>
internal static class Ui
{
    private static Sprite rounded;
    private static TMP_FontAsset font;
    private static bool fontLogged;

    public static Sprite Rounded
    {
        get
        {
            if (rounded) return rounded;
            const int size = 64, radius = 20;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Distancia al borde de un rectángulo redondeado, con 1 px de antialias.
                var cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                var cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                var a = Mathf.Clamp01(radius - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            rounded.hideFlags = HideFlags.HideAndDontSave;
            return rounded;
        }
    }

    /// <summary>Fuente del propio juego si se encuentra una legible; si no, la de TextMeshPro.</summary>
    public static TMP_FontAsset Font
    {
        get
        {
            if (font) return font;
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (!fontLogged)
            {
                fontLogged = true;
                Plugin.Log.LogDebug("Fuentes TMP: " + string.Join(", ", all.Select(f => f.name)));
            }
            foreach (var wanted in PreferredFonts)
            {
                font = all.FirstOrDefault(f => f && f.name.Equals(wanted, System.StringComparison.OrdinalIgnoreCase));
                if (font) return font;
            }
            font = TMP_Settings.defaultFontAsset;
            if (!font) font = all.FirstOrDefault(f => f);
            return font;
        }
    }

    // El juego trae LiberationSans (la de TextMeshPro) para texto y Teko para su HUD.
    private static readonly string[] PreferredFonts = { "LiberationSans SDF" };

    private static TMP_FontAsset display;

    /// <summary>Teko, la fuente estrecha del HUD del juego: para títulos y cifras grandes.</summary>
    public static TMP_FontAsset DisplayFont
    {
        get
        {
            if (display) return display;
            display = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(f => f && f.name.StartsWith("Teko", System.StringComparison.OrdinalIgnoreCase));
            return display ? display : Font;
        }
    }

    public static TextMeshProUGUI Display(this TextMeshProUGUI t)
    {
        t.font = DisplayFont;
        return t;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent ? parent.gameObject.layer : 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static RectTransform Fill(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    /// <summary>Posición absoluta desde la esquina superior izquierda del padre.</summary>
    public static RectTransform Place(this RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static Image Box(Transform parent, Color color, string name = "Box", bool round = true, float radiusScale = 1f)
    {
        var rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (round)
        {
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.05f, radiusScale);
        }
        return img;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, string name = "Label")
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        var f = Font;
        if (f) t.font = f;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    public static void Clear(Transform t)
    {
        for (var i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }
}
