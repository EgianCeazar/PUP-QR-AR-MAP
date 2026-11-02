// Menu: PUP Map > Build Phase 1 Scene
// Creates the whole map screen (camera, map, UI) so you don't have to wire it by hand.
using PUPMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class Phase1SceneBuilder
{
    static readonly Color Maroon = new Color(0.48f, 0.0f, 0.0f);
    static readonly Color Ink = new Color(0.13f, 0.13f, 0.15f);
    static readonly Color Muted = new Color(0.45f, 0.45f, 0.48f);
    static readonly Color Card = new Color(1f, 1f, 1f, 0.97f);
    static readonly Color Field = new Color(0.95f, 0.95f, 0.96f);
    static readonly Color Blue = new Color(0.12f, 0.53f, 0.95f);
    static readonly Color Green = new Color(0.10f, 0.70f, 0.32f);

    static DefaultControls.Resources res;
    static Font font;

    [MenuItem("PUP Map/Build Phase 1 Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        res = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ---------- map + camera ----------
        var app = new GameObject("App");
        var loader = app.AddComponent<MapLoader>();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.75f, 0.92f); // river blue around the map
        camGo.transform.position = new Vector3(0, 0, -100);

        var mapRoot = new GameObject("Map");
        var view = mapRoot.AddComponent<MapView2D>();
        view.loader = loader;
        view.mapCamera = cam;

        var camCtrl = camGo.AddComponent<MapCameraController>();
        camCtrl.view = view;
        camCtrl.topPanel = 0.26f;
        camCtrl.bottomPanel = 0.31f;

        // ---------- UI ----------
        var es = new GameObject("EventSystem", typeof(EventSystem));
        AddInputModule(es);

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); // portrait phone
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = (RectTransform)canvasGo.transform;

        // Top card
        var top = Box(canvas, "Top Card", Card, true);
        Anchor(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -470), new Vector2(-24, -24));

        var title = Label(top, "Title", "PUP Campus Map", 44, Maroon, FontStyle.Bold, TextAnchor.MiddleLeft);
        Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(32, -100), new Vector2(0, -16));

        // 2D / AR segmented switch
        var seg = Box(top, "View Switch", Field, false);
        Anchor(seg, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-372, -92), new Vector2(-28, -24));
        var mapBtn = PillButton(seg, "2D Map", new Vector2(0, 0), new Vector2(0.5f, 1));
        var arBtn = PillButton(seg, "AR (soon)", new Vector2(0.5f, 0), new Vector2(1, 1));

        // From / To with colored dots, swap button on the right
        var startDd = PlaceRow(top, "From", Blue, -112);
        var destDd = PlaceRow(top, "To", Green, -220);

        var swap = MakeButton(top, "Swap", "↑↓", 44, Maroon, Color.white);
        Anchor((RectTransform)swap.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-120, -310), new Vector2(-28, -112));

        // Test QR row (Phase 1 stand-in for the camera scanner)
        var qrRow = new GameObject("QR Test Row", typeof(RectTransform)).GetComponent<RectTransform>();
        qrRow.SetParent(top, false);
        Anchor(qrRow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -420), new Vector2(-28, -336));
        var inputGo = DefaultControls.CreateInputField(res);
        inputGo.name = "QR Input";
        inputGo.transform.SetParent(qrRow, false);
        Anchor((RectTransform)inputGo.transform, Vector2.zero, new Vector2(0.7f, 1), Vector2.zero, Vector2.zero);
        inputGo.GetComponent<Image>().color = Field;
        var input = inputGo.GetComponent<InputField>();
        Style(input.textComponent, 30, Ink);
        var ph = (Text)input.placeholder; Style(ph, 28, Muted); ph.fontStyle = FontStyle.Italic;
        ph.text = "Test a QR scan: WAYFIND:1:pup-main:lagoon";
        var qrBtn = MakeButton(qrRow, "Use QR", "Use QR", 30, Field, Ink);
        Anchor((RectTransform)qrBtn.transform, new Vector2(0.72f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        // Bottom sheet
        var sheet = Box(canvas, "Bottom Sheet", Card, true);
        Anchor(sheet, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 600));
        var handle = Box(sheet, "Handle", new Color(0.82f, 0.82f, 0.84f), false);
        Anchor(handle, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-50, -30), new Vector2(50, -20));

        var summary = Label(sheet, "Summary", "Loading map…", 42, Ink, FontStyle.Normal, TextAnchor.UpperLeft);
        summary.supportRichText = true;
        Anchor(summary.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -160), new Vector2(-40, -48));

        var divider = Box(sheet, "Divider", new Color(0.9f, 0.9f, 0.92f), false);
        Anchor(divider, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -172), new Vector2(-40, -168));

        var steps = ScrollingText(sheet, out var scrollRt);
        Anchor(scrollRt, new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 30), new Vector2(-40, -184));

        // ---------- wire it up ----------
        var nav = app.AddComponent<NavigationController>();
        nav.loader = loader; nav.view = view; nav.mapCamera = camCtrl;
        nav.startDropdown = startDd; nav.destinationDropdown = destDd; nav.swapButton = swap;
        nav.qrInput = input; nav.qrButton = qrBtn;
        nav.summaryText = summary; nav.stepsText = steps;

        var mode = app.AddComponent<ViewModeController>();
        mode.map2DObjects = new[] { mapRoot, camGo };
        mode.arObjects = new GameObject[0]; // Phase 2 adds the AR session + AR camera here
        mode.mapButton = mapBtn;
        mode.arButton = arBtn;
        mode.arButtonLabel = arBtn.GetComponentInChildren<Text>();
        mode.stepsText = steps;
        mode.selectedColor = Maroon;
        mode.normalColor = Field;

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Phase1_Map.unity");
        Debug.Log("PUP Map: scene created at Assets/Scenes/Phase1_Map.unity. Press Play!");
    }

    // ---------- building blocks ----------

    static Dropdown PlaceRow(RectTransform parent, string label, Color dot, float top)
    {
        var row = new GameObject(label + " Row", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        Anchor(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, top - 96), new Vector2(-136, top));

        var dotImg = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        dotImg.transform.SetParent(row, false);
        dotImg.sprite = res.knob; dotImg.color = dot;
        Anchor(dotImg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, -14), new Vector2(34, 14));

        var cap = Label(row, label + " Label", label, 30, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        Anchor(cap.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(48, 0), new Vector2(138, 0));

        var go = DefaultControls.CreateDropdown(res);
        go.name = label + " Dropdown";
        go.transform.SetParent(row, false);
        Anchor((RectTransform)go.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(140, 6), new Vector2(0, -6));
        go.GetComponent<Image>().color = Field;
        var dd = go.GetComponent<Dropdown>();
        Style(dd.captionText, 34, Ink);
        dd.captionText.resizeTextForBestFit = true; dd.captionText.resizeTextMinSize = 22; dd.captionText.resizeTextMaxSize = 34;
        Style(dd.itemText, 34, Ink);
        dd.template.sizeDelta = new Vector2(0, 760);
        var item = dd.template.GetComponentInChildren<Toggle>(true).GetComponent<RectTransform>();
        item.sizeDelta = new Vector2(item.sizeDelta.x, 84);
        return dd;
    }

    static Button PillButton(RectTransform parent, string text, Vector2 aMin, Vector2 aMax)
    {
        var b = MakeButton(parent, text, text, 30, Field, Ink);
        Anchor((RectTransform)b.transform, aMin, aMax, new Vector2(4, 4), new Vector2(-4, -4));
        return b;
    }

    static Button MakeButton(RectTransform parent, string name, string text, int size, Color bg, Color fg)
    {
        var go = DefaultControls.CreateButton(res);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        var t = go.GetComponentInChildren<Text>();
        t.text = text; Style(t, size, fg); t.fontStyle = FontStyle.Bold;
        return go.GetComponent<Button>();
    }

    static RectTransform Box(RectTransform parent, string name, Color c, bool shadow)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = res.standard; img.type = Image.Type.Sliced; img.color = c;
        if (shadow)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.18f); s.effectDistance = new Vector2(0, -6);
        }
        return (RectTransform)go.transform;
    }

    static Text Label(RectTransform parent, string name, string text, int size, Color c, FontStyle style, TextAnchor anchor)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.text = text; Style(t, size, c); t.fontStyle = style; t.alignment = anchor;
        return t;
    }

    static Text ScrollingText(RectTransform parent, out RectTransform scrollRt)
    {
        var sv = DefaultControls.CreateScrollView(res);
        sv.name = "Directions";
        sv.transform.SetParent(parent, false);
        scrollRt = (RectTransform)sv.transform;
        sv.GetComponent<Image>().color = new Color(1, 1, 1, 0);
        var sr = sv.GetComponent<ScrollRect>();
        sr.horizontal = false;
        if (sr.horizontalScrollbar) Object.DestroyImmediate(sr.horizontalScrollbar.gameObject);
        if (sr.verticalScrollbar) Object.DestroyImmediate(sr.verticalScrollbar.gameObject);
        sr.viewport.offsetMin = Vector2.zero; sr.viewport.offsetMax = Vector2.zero;

        var content = sr.content;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var t = Label(content, "Steps", "", 34, Ink, FontStyle.Normal, TextAnchor.UpperLeft);
        t.supportRichText = true;
        t.lineSpacing = 1.25f;
        return t;
    }

    static void Style(Text t, int size, Color c) { t.font = font; t.fontSize = size; t.color = c; }

    static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    static void AddInputModule(GameObject es)
    {
        // Use the new Input System's UI module if the package is installed, otherwise the old one.
        var newModule = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newModule != null) es.AddComponent(newModule);
        else es.AddComponent<StandaloneInputModule>();
    }
}
