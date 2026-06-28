using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Majinfwork.World {
    internal static class FrameworkEditorIcons {
        private const string IconPath = "Packages/com.majingari.framework/Main/Editor Default Resources/Icons/PlayFramework.png";

        private static Texture2D playFrameworkIcon;
        public static Texture2D PlayFramework => playFrameworkIcon ??= AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
    }

    /// <summary>
    /// Scene View overlay button — uses the [EditorToolbarElement] + Overlay API.
    /// </summary>
    [Overlay(typeof(SceneView), "Play Framework", true)]
    public class PlayFrameworkOverlay : ToolbarOverlay {
        public PlayFrameworkOverlay() : base(PlayWithFrameworkButton.Id) { }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    public class PlayWithFrameworkButton : EditorToolbarButton {
        public const string Id = "Majinfwork/PlayWithFramework";

        public PlayWithFrameworkButton() {
            text = "Play Framework";
            tooltip = "Enter Play Mode with Framework enabled";
            if (FrameworkEditorIcons.PlayFramework != null)
                icon = FrameworkEditorIcons.PlayFramework;
            clicked += PlayWithFramework.Toggle;
        }
    }

    /// <summary>
    /// Main toolbar button — uses the Unity 6 supported [MainToolbarElement] API
    /// (UnityEditor.Toolbars.MainToolbarElementAttribute / MainToolbarButton).
    /// This replaces the previous reflection-based injection into the private
    /// UnityEditor.Toolbar.m_Root field, which Unity 6.x actively rejects and which
    /// caused "framework didn't boot" / "TitleScreen widget missing" bugs when the
    /// injection silently failed.
    /// </summary>
    internal static class PlayFrameworkMainToolbar {
        [MainToolbarElement("Majinfwork/PlayWithFramework", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement Create() {
            var content = new MainToolbarContent {
                image = FrameworkEditorIcons.PlayFramework,
                text = FrameworkEditorIcons.PlayFramework == null ? "Framework" : null,
                tooltip = "Play with Framework enabled (Ctrl+Alt+P)"
            };
            return new MainToolbarButton(content, PlayWithFramework.Toggle);
        }
    }

    /// <summary>
    /// Core entry points for entering Play Mode with the framework flag set.
    /// Exposed as a menu item, a Scene View overlay button, and a main toolbar button.
    /// </summary>
    public static class PlayWithFramework {
        [MenuItem("Majingari Framework/Play With Framework %&p", priority = -100)]
        public static void Toggle() {
            if (EditorApplication.isPlaying) {
                EditorApplication.isPlaying = false;
            }
            else {
                SessionState.SetBool(GameWorldSession.PlayWithFrameworkKey, true);
                EditorApplication.isPlaying = true;
            }
        }
    }

    /// <summary>
    /// Clears the framework-play flag on exiting play mode so the next regular Play
    /// doesn't accidentally boot the framework.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayFrameworkFlagLifetime {
        static PlayFrameworkFlagLifetime() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingPlayMode) {
                SessionState.SetBool(GameWorldSession.PlayWithFrameworkKey, false);
            }
        }
    }
}
