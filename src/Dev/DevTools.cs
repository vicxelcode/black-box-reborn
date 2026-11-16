#if BBR_DEV
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BlackBoxReborn.Stats;
using BlackBoxReborn.UI;
using UnityEngine;

namespace BlackBoxReborn.Dev;

/// <summary>
/// Solo en Debug. Permite manejar el juego desde fuera escribiendo órdenes en
/// BepInEx/bbr-dev/cmd.txt y deja capturas y un estado legible en esa carpeta. Sirve para probar sin
/// quitarle el foco a nadie.
/// </summary>
internal sealed class DevTools : MonoBehaviour
{
    private string dir;
    private float nextPoll;
    private float nextStatus;
    private readonly Queue<string> pending = new();
    private float waitUntil;
    private int snapW = 1600, snapH = 900;

    private void Awake()
    {
        dir = Path.Combine(Paths.BepInExRootPath, "bbr-dev");
        Directory.CreateDirectory(dir);
        Application.runInBackground = true;
        Plugin.Log.LogInfo("DevTools activas: órdenes en " + dir);
    }

    private void Update()
    {
        try
        {
            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 0.5f;
                var cmd = Path.Combine(dir, "cmd.txt");
                if (File.Exists(cmd))
                {
                    foreach (var line in File.ReadAllLines(cmd)) if (!string.IsNullOrWhiteSpace(line)) pending.Enqueue(line.Trim());
                    File.Delete(cmd);
                }
            }
            while (pending.Count > 0 && Time.unscaledTime >= waitUntil) Run(pending.Dequeue());
            if (Time.unscaledTime >= nextStatus)
            {
                nextStatus = Time.unscaledTime + 1f;
                File.WriteAllText(Path.Combine(dir, "status.txt"), Status());
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("DevTools: " + e);
        }
    }

    private void Run(string line)
    {
        Plugin.Log.LogInfo("dev> " + line);
        var a = line.Split(' ');
        var controller = GetComponent<Controller>();
        switch (a[0])
        {
            case "wait":
                waitUntil = Time.unscaledTime + F(a[1]);
                break;
            case "start":
                SemiFunc.MenuActionSingleplayerGame();
                break;
            case "level":
                RunManager.instance.ChangeLevel(true, false);
                break;
            case "shop":
                RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.Shop);
                break;
            case "shot":
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, a[1] + ".png"));
                break;
            case "panel":
                controller.Panel.Toggle();
                break;
            case "tab":
                controller.Panel.SetTab(int.Parse(a[1]));
                break;
            case "sel":
                controller.Panel.ChangeSelection(int.Parse(a[1]));
                break;
            case "lang":
                Settings.Language.Value = a[1] == "es" ? UiLanguage.Spanish : UiLanguage.English;
                break;
            case "mon":
                TruckMonitor.LocalOffset = new Vector3(F(a[1]), F(a[2]), F(a[3]));
                TruckMonitor.LocalEuler = new Vector3(F(a[4]), F(a[5]), F(a[6]));
                if (a.Length > 7) TruckMonitor.WorldWidth = F(a[7]);
                if (controller.Monitor) controller.Monitor.ApplyPlacement();
                break;
            case "res":
                snapW = int.Parse(a[1]);
                snapH = int.Parse(a[2]);
                break;
            case "snap":
                Snap(a);
                break;
            case "break":
                Break(a.Length > 1 ? int.Parse(a[1]) : 1);
                break;
            case "extract":
                Extract(a.Length > 1 ? int.Parse(a[1]) : 1);
                break;
            case "hold":
                Hold(int.Parse(a[1]), F(a[2]));
                break;
            case "breakheld":
                foreach (var v in held) BreakOne(v, a.Length > 1 && a[1] == "heavy");
                break;
            case "extractheld":
                foreach (var v in held) if (v) v.GetComponent<PhysGrabObject>().DestroyPhysGrabObject();
                break;
            case "activate":
                var point = RoundDirector.instance.extractionPointList.FirstOrDefault(e => e);
                if (point) point.GetComponent<ExtractionPoint>().OnClick();
                break;
            case "hurt":
                if (PlayerAvatar.instance) PlayerAvatar.instance.playerHealth.Hurt(int.Parse(a[1]), false);
                break;
            case "dump":
                File.WriteAllText(Path.Combine(dir, "dump.txt"), Dump());
                break;
            case "force":
                // force <texto del nombre del nivel> | force off — fija el siguiente nivel (RunManager.debugLevel)
                RunManager.instance.debugLevel = a[1] == "off"
                    ? null
                    : RunManager.instance.levels.FirstOrDefault(l => l && l.name.IndexOf(a[1], StringComparison.OrdinalIgnoreCase) >= 0);
                Plugin.Log.LogInfo("debugLevel = " + (RunManager.instance.debugLevel ? RunManager.instance.debugLevel.name : "-"));
                break;
            case "uidump":
                File.WriteAllText(Path.Combine(dir, "uidump.txt"), UiDump());
                break;
            case "menu":
                MenuManager.instance.PageOpen((MenuPageIndex)int.Parse(a[1]));
                break;
            case "quit":
                Application.Quit();
                break;
            default:
                Plugin.Log.LogWarning("dev: orden desconocida " + a[0]);
                break;
        }
    }

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>
    /// snap &lt;nombre&gt; monitor [distancia] | screen [distancia] | x y z rx ry rz (relativo al camión).
    /// Renderiza con una cámara propia a una textura y la guarda en PNG: no toca las cámaras del juego.
    /// </summary>
    private void Snap(string[] a)
    {
        var name = a[1];
        var mode = a[2];
        var go = new GameObject("BBR SnapCam");
        var cam = go.AddComponent<Camera>();
        if (Camera.main) cam.CopyFrom(Camera.main);
        cam.enabled = false;
        cam.nearClipPlane = 0.02f;
        cam.fieldOfView = 55f;
        var monitor = GetComponent<Controller>().Monitor;
        if (mode == "monitor" && monitor)
        {
            var t = monitor.transform;
            var distance = a.Length > 3 ? F(a[3]) : 2.4f;
            go.transform.position = t.position - t.forward * distance;
            go.transform.rotation = Quaternion.LookRotation(t.forward, Vector3.up);
        }
        else if (mode == "screen" && TruckScreenText.instance)
        {
            var t = TruckScreenText.instance.transform;
            var distance = a.Length > 3 ? F(a[3]) : 1.5f;
            go.transform.position = t.position - t.forward * distance;
            go.transform.rotation = Quaternion.LookRotation(t.forward, Vector3.up);
        }
        else
        {
            var anchor = TruckScreenText.instance ? TruckScreenText.instance.transform.parent : null;
            var pos = new Vector3(F(a[2]), F(a[3]), F(a[4]));
            var rot = Quaternion.Euler(F(a[5]), F(a[6]), F(a[7]));
            go.transform.position = anchor ? anchor.TransformPoint(pos) : pos;
            go.transform.rotation = anchor ? anchor.rotation * rot : rot;
        }

        var rt = new RenderTexture(snapW, snapH, 24);
        cam.targetTexture = rt;
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = null;
        Destroy(rt);
        Destroy(tex);
        Destroy(go);
    }

    private readonly List<ValuableObject> held = new();
    private float holdUntil;

    /// <summary>Simula que el jugador local agarra n objetos durante un rato (sin física: solo la lista de agarres).</summary>
    private void Hold(int count, float seconds)
    {
        held.Clear();
        held.AddRange(UnityEngine.Object.FindObjectsByType<ValuableObject>(FindObjectsSortMode.None)
            .Where(v => v.dollarValueSet).Take(count));
        holdUntil = Time.time + seconds;
    }

    private void LateUpdate()
    {
        if (Time.time >= holdUntil || !PlayerAvatar.instance) return;
        var grabber = PlayerAvatar.instance.physGrabber;
        foreach (var v in held)
        {
            if (!v) continue;
            var list = v.GetComponent<PhysGrabObject>().playerGrabbing;
            if (!list.Contains(grabber)) list.Add(grabber);
        }
    }

    private static void BreakOne(ValuableObject v, bool heavy)
    {
        if (!v) return;
        var detector = v.GetComponent<PhysGrabObjectImpactDetector>();
        if (detector) detector.Break(heavy ? v.dollarValueCurrent : v.dollarValueOriginal * 0.1f, v.transform.position, heavy ? 3 : 1, heavy);
    }

    private static void Break(int count)
    {
        var all = UnityEngine.Object.FindObjectsByType<ValuableObject>(FindObjectsSortMode.None).Where(v => v.dollarValueSet).ToList();
        for (var i = 0; i < count && all.Count > 0; i++)
        {
            var v = all[UnityEngine.Random.Range(0, all.Count)];
            var detector = v.GetComponent<PhysGrabObjectImpactDetector>();
            if (!detector) continue;
            var heavy = UnityEngine.Random.value < 0.3f;
            detector.Break(heavy ? v.dollarValueCurrent : v.dollarValueOriginal * 0.1f, v.transform.position, heavy ? 3 : 1, heavy);
        }
    }

    private static void Extract(int count)
    {
        var all = UnityEngine.Object.FindObjectsByType<ValuableObject>(FindObjectsSortMode.None).Where(v => v.dollarValueSet).Take(count).ToList();
        foreach (var v in all) v.GetComponent<PhysGrabObject>().DestroyPhysGrabObject();
    }

    private static string Status()
    {
        var sb = new StringBuilder();
        var rm = RunManager.instance;
        sb.AppendLine($"time={Time.unscaledTime:0} level={(rm && rm.levelCurrent ? rm.levelCurrent.name : "-")} " +
                      $"isLevel={SemiFunc.RunIsLevel()} isShop={SemiFunc.RunIsShop()} " +
                      $"gen={(LevelGenerator.Instance ? LevelGenerator.Instance.Generated.ToString() : "-")} " +
                      $"state={(GameDirector.instance ? GameDirector.instance.currentState.ToString() : "-")}");
        sb.AppendLine($"screen={(TruckScreenText.instance ? TruckScreenText.instance.transform.position.ToString("F2") : "-")} " +
                      $"player={(PlayerAvatar.instance ? PlayerAvatar.instance.transform.position.ToString("F2") : "-")}");
        var l = Tracker.Run.Current;
        if (l != null)
        {
            sb.AppendLine($"L{l.Number} {l.LevelName} finished={l.Finished} spawned={l.ValueSpawned:0}/{l.ItemsSpawned} " +
                          $"extracted={l.ValueExtracted:0}/{l.ItemsExtracted} lost={l.ValueLost:0} destroyed={l.ItemsDestroyed} " +
                          $"discovered={l.ItemsDiscovered} remaining={l.ValueRemaining:0} deaths={l.Deaths}");
            foreach (var p in l.Players.Values)
                sb.AppendLine($"  {p.Id} {p.Name} hauled={p.ValueHauled:0} broken={p.ValueBroken:0} hits={p.Hits} hurt={p.DamageTaken} deaths={p.Deaths}");
        }
        return sb.ToString();
    }

    /// <summary>Cómo está hecha la interfaz del juego: lienzos, textos, materiales y la cabeza de jugador.</summary>
    private static string UiDump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("== Lienzos");
        foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>().Where(c => c.gameObject.scene.IsValid()))
            sb.AppendLine($"{NodePath(c.transform)} mode={c.renderMode} cam={(c.worldCamera ? c.worldCamera.name : "-")} layer={c.gameObject.layer} order={c.sortingOrder} active={c.gameObject.activeInHierarchy} " +
                          $"scaler={(c.GetComponent<UnityEngine.UI.CanvasScaler>() is { } s ? s.uiScaleMode + " " + s.referenceResolution + " ppu=" + s.referencePixelsPerUnit + " sf=" + s.scaleFactor : "-")} size={((RectTransform)c.transform).sizeDelta}");
        sb.AppendLine("== Textos activos");
        foreach (var t in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            sb.AppendLine($"{NodePath(t.transform)} font={(t.font ? t.font.name : "-")} mat={(t.fontSharedMaterial ? t.fontSharedMaterial.name + " / " + t.fontSharedMaterial.shader.name : "-")} size={t.fontSize} color={ColorUtility.ToHtmlStringRGBA(t.color)} style={t.fontStyle} spacing={t.characterSpacing} text={Snippet(t.text)} comps={string.Join(",", t.GetComponents<Component>().Select(x => x.GetType().Name))}");
        sb.AppendLine("== Materiales de TMP");
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>().Where(m => m.shader && m.shader.name.Contains("TextMeshPro")))
            sb.AppendLine($"{m.name} shader={m.shader.name} outline={(m.HasProperty("_OutlineWidth") ? m.GetFloat("_OutlineWidth") : -1)} glow={(m.HasProperty("_GlowPower") ? m.GetFloat("_GlowPower") : -1)} under={(m.HasProperty("_UnderlayOffsetX") ? m.GetFloat("_UnderlayOffsetX") + "," + m.GetFloat("_UnderlayOffsetY") : "-")} kw={string.Join(" ", m.shaderKeywords)}");
        sb.AppendLine("== Imágenes activas");
        foreach (var g in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None).Where(g => !(g is TMPro.TMP_Text)))
        {
            var tex = g is UnityEngine.UI.RawImage r ? (r.texture ? r.texture.name : "-") : g is UnityEngine.UI.Image i ? (i.sprite ? i.sprite.name : "-") : "-";
            sb.AppendLine($"{NodePath(g.transform)} {g.GetType().Name} tex={tex} mat={(g.material ? g.material.name + "/" + g.material.shader.name : "-")} color={ColorUtility.ToHtmlStringRGBA(g.color)}");
        }
        sb.AppendLine("== Prefab MenuPlayerHead");
        var head = Resources.FindObjectsOfTypeAll<MenuPlayerHead>().FirstOrDefault();
        if (head) DumpUi(sb, head.transform, 0);
        sb.AppendLine("== Colores de jugador (MetaManager)");
        if (MetaManager.instance) sb.AppendLine(string.Join(" ", MetaManager.instance.colors.Select(c => ColorUtility.ToHtmlStringRGB(c.color))));
        return sb.ToString();
    }

    private static string Snippet(string text)
    {
        var flat = (text ?? "").Replace('\n', ' ');
        return "'" + flat.Substring(0, Math.Min(60, flat.Length)) + "'";
    }

    private static string NodePath(Transform t)
    {
        var parts = new List<string>();
        for (var p = t; p; p = p.parent) parts.Add(p.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void DumpUi(StringBuilder sb, Transform t, int depth)
    {
        var rt = t as RectTransform;
        var g = t.GetComponent<UnityEngine.UI.Graphic>();
        var tex = g is UnityEngine.UI.RawImage r ? (r.texture ? r.texture.name + " " + r.texture.width + "x" + r.texture.height : "-") : g is UnityEngine.UI.Image i ? (i.sprite ? i.sprite.name : "-") : "";
        sb.AppendLine($"{new string(' ', depth * 2)}{t.name} [{string.Join(",", t.GetComponents<Component>().Select(x => x.GetType().Name))}] {(rt ? "pos=" + rt.anchoredPosition + " size=" + rt.sizeDelta : "")} {tex}");
        foreach (Transform c in t) DumpUi(sb, c, depth + 1);
    }

    private static string Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fuentes: " + string.Join(", ", Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().Select(f => f.name)));
        var screen = TruckScreenText.instance;
        if (screen)
        {
            var t = screen.transform;
            sb.AppendLine("Cadena de padres de TruckScreenText:");
            for (var p = t; p; p = p.parent)
                sb.AppendLine($"  {p.name} pos={p.position:F2} rot={p.eulerAngles:F1} scale={p.lossyScale:F3} fwd={p.forward:F2}");
            var root = t.root;
            sb.AppendLine("Árbol de " + root.name + ":");
            DumpTree(sb, root, 0, 4);
        }
        foreach (var c in Camera.allCameras)
            sb.AppendLine($"Cámara {c.name} depth={c.depth} enabled={c.enabled} rt={(c.targetTexture ? c.targetTexture.name + " " + c.targetTexture.width + "x" + c.targetTexture.height : "-")} mask={c.cullingMask} main={c == Camera.main} pos={c.transform.position:F2}");
        var cctv = GameObject.Find("cctv");
        if (cctv) sb.AppendLine($"cctv pos={cctv.transform.position:F2} rot={cctv.transform.eulerAngles:F1}");
        return sb.ToString();
    }

    private static void DumpTree(StringBuilder sb, Transform t, int depth, int max)
    {
        var renderer = t.GetComponent<Renderer>();
        var bounds = renderer ? $" bounds={renderer.bounds.center:F2}/{renderer.bounds.size:F2}" : "";
        sb.AppendLine($"{new string(' ', depth * 2)}{t.name} lpos={t.localPosition:F2} pos={t.position:F2} rot={t.eulerAngles:F0}{bounds}");
        if (depth >= max) return;
        foreach (Transform c in t) DumpTree(sb, c, depth + 1, max);
    }
}
#endif
