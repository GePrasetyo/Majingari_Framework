using UnityEngine;
using UnityEngine.UIElements;

namespace Majinfwork.World {
    /// <summary>
    /// Runtime-created UI Toolkit panel for the loading-screen overlay.
    /// PanelSettings + UIDocument are built inside <see cref="Initialize"/>, not Awake,
    /// so the theme can be injected from the caller without Unity warning about a
    /// missing theme style sheet.
    /// </summary>
    internal class RuntimeLoadingPanel : MonoBehaviour {
        private UIDocument uiDocument;
        private PanelSettings panelSettings;
        private VisualElement overlay;
        private bool initialized;

        /// <summary>
        /// Build PanelSettings, UIDocument, and the visual tree.
        /// Pass the project's runtime theme (e.g. UnityDefaultRuntimeTheme) so UI Toolkit
        /// can style the panel. Null theme is tolerated (loading overlay is just a black
        /// rectangle so it still renders) but Unity will log a theme-missing warning.
        /// </summary>
        public void Initialize(ThemeStyleSheet theme) {
            if (initialized) return;

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "LoadingPanelSettings";
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            // High sort order to render on top
            panelSettings.sortingOrder = 10000;
            if (theme != null) {
                panelSettings.themeStyleSheet = theme;
            }

            uiDocument = gameObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;

            BuildVisualTree();
        }

        private void BuildVisualTree() {
            if (initialized) return;

            var root = uiDocument.rootVisualElement;
            if (root == null) {
                Debug.LogError("[RuntimeLoadingPanel] rootVisualElement is null - UI Toolkit panel failed to initialize");
                return;
            }

            // Make root fill the screen
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            // Root is a transparent container — overlay handles all picking
            root.pickingMode = PickingMode.Ignore;

            overlay = new VisualElement {
                name = "loading-overlay",
                pickingMode = PickingMode.Ignore,
                style = {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    backgroundColor = Color.black,
                    opacity = 0,
                    display = DisplayStyle.None
                }
            };

            root.Add(overlay);
            initialized = true;
        }

        public void Show() {
            if (!initialized) return;
            if (overlay != null) {
                overlay.style.display = DisplayStyle.Flex;
            }
        }

        public void Hide() {
            if (overlay != null) {
                overlay.style.display = DisplayStyle.None;
            }
        }

        public void SetOpacity(float opacity) {
            if (!initialized) return;
            if (overlay != null) {
                overlay.style.opacity = opacity;
            }
        }

        public float GetOpacity() {
            if (!initialized) return 0f;
            if (overlay != null) {
                return overlay.resolvedStyle.opacity;
            }
            return 0f;
        }
    }
}
