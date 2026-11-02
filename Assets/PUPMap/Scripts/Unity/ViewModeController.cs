// Lets the user pick between the 2D map and the AR camera view.
// - The choice is saved on the phone, so the app remembers it next time.
// - 2D map is the default, and it's always available.
// - If the phone can't do AR (or AR isn't built yet), the AR button is disabled
//   and the app stays on the 2D map.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PUPMap
{
    public enum ViewMode { Map2D = 0, AR = 1 }

    public class ViewModeController : MonoBehaviour
    {
        const string PrefKey = "PUPMap.ViewMode";

        [Header("What to show in each mode")]
        public GameObject[] map2DObjects;  // map drawing + top-down camera
        public GameObject[] arObjects;     // AR session + AR camera (added in Phase 2)

        [Header("UI")]
        public Button mapButton;
        public Button arButton;
        public Text arButtonLabel;
        public Text stepsText;             // used to show a short message on fallback

        public Color selectedColor = new Color(0.47f, 0f, 0f);    // PUP maroon
        public Color normalColor = new Color(0.9f, 0.9f, 0.9f);

        /// <summary>
        /// Set by the AR code in Phase 2 (e.g. after checking ARSession.state).
        /// Until then, AR counts as "not available".
        /// </summary>
        public static Func<bool> IsArSupported = () => false;

        public ViewMode Mode { get; private set; } = ViewMode.Map2D;
        public event Action<ViewMode> ModeChanged;

        void Start()
        {
            if (mapButton) mapButton.onClick.AddListener(() => SetMode(ViewMode.Map2D));
            if (arButton) arButton.onClick.AddListener(() => SetMode(ViewMode.AR));
            RefreshArButton();

            var saved = (ViewMode)PlayerPrefs.GetInt(PrefKey, (int)ViewMode.Map2D);
            SetMode(saved, save: false);
        }

        public void SetMode(ViewMode mode) => SetMode(mode, save: true);

        void SetMode(ViewMode mode, bool save)
        {
            if (mode == ViewMode.AR && !IsArSupported())
            {
                if (save && stepsText) stepsText.text = "AR isn't available on this phone yet. Using the 2D map.";
                mode = ViewMode.Map2D;
            }

            Mode = mode;
            SetActive(map2DObjects, mode == ViewMode.Map2D);
            SetActive(arObjects, mode == ViewMode.AR);
            Highlight(mapButton, mode == ViewMode.Map2D);
            Highlight(arButton, mode == ViewMode.AR);

            if (save) { PlayerPrefs.SetInt(PrefKey, (int)mode); PlayerPrefs.Save(); }
            ModeChanged?.Invoke(mode);
        }

        /// <summary>Call again if AR support changes (e.g. after ARCore finishes checking).</summary>
        public void RefreshArButton()
        {
            bool ok = IsArSupported();
            if (arButton) arButton.interactable = ok;
            if (arButtonLabel) arButtonLabel.text = ok ? "AR View" : "AR View (soon)";
            if (!ok && Mode == ViewMode.AR) SetMode(ViewMode.Map2D, save: false);
        }

        static void SetActive(GameObject[] objs, bool on)
        {
            if (objs == null) return;
            foreach (var o in objs) if (o) o.SetActive(on);
        }

        void Highlight(Button b, bool on)
        {
            if (!b) return;
            var img = b.GetComponent<Image>();
            if (img) img.color = on ? selectedColor : normalColor;
            var label = b.GetComponentInChildren<Text>();
            if (label) label.color = on ? Color.white : Color.black;
        }
    }
}
