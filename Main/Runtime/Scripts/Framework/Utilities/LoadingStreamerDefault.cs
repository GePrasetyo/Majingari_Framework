using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Majinfwork.World {
    /// <summary>
    /// Default loading screen using UI Toolkit.
    /// Creates all UI elements at runtime - no prefabs, UXML, or USS required.
    /// Assign a runtime ThemeStyleSheet (e.g. UnityDefaultRuntimeTheme) so UI Toolkit
    /// can style the loading panel; without one, Unity logs a warning on boot.
    /// </summary>
    [Serializable]
    public class LoadingStreamerDefault : LoadingStreamer {
        [SerializeField, Min(0.1f)] private float fadeSpeed = 1;

        [Tooltip("Runtime theme used by the loading-screen PanelSettings. " +
                 "Assign your project's default (e.g. Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss). " +
                 "Leave null only if you know you don't need a theme.")]
        [SerializeField] private ThemeStyleSheet loadingTheme;

        private RuntimeLoadingPanel panel;
        private bool constructed;

        // Coalescing: track current fade operations
        private Task currentFadeIn;
        private Task currentFadeOut;

        // Internal cancellation for ForceCancel
        private CancellationTokenSource fadeCts;

        protected override void Construct() {
            var go = new GameObject("LoadingPanel");
            panel = go.AddComponent<RuntimeLoadingPanel>();
            panel.Initialize(loadingTheme);
            UnityEngine.Object.DontDestroyOnLoad(go);
            constructed = true;
        }

        public override async Task StartLoadingAsync(CancellationToken cancellationToken = default) {
            if (!constructed || panel == null) return;

            // If already fading in, just wait for it (with caller's cancellation)
            if (currentFadeIn != null && !currentFadeIn.IsCompleted) {
                await AwaitWithCancellation(currentFadeIn, cancellationToken);
                return;
            }

            panel.Show();

            // Create new internal CTS for this fade operation
            fadeCts?.Dispose();
            fadeCts = new CancellationTokenSource();

            currentFadeIn = FadeAsync(1f, fadeCts.Token);

            try {
                await AwaitWithCancellation(currentFadeIn, cancellationToken);
            }
            catch (OperationCanceledException) when (fadeCts.IsCancellationRequested) {
                // ForceCancel was called - cleanup already done
                throw;
            }
        }

        public override async Task StopLoadingAsync(CancellationToken cancellationToken = default) {
            if (!constructed || panel == null) return;

            // If already fading out, just wait for it (with caller's cancellation)
            if (currentFadeOut != null && !currentFadeOut.IsCompleted) {
                await AwaitWithCancellation(currentFadeOut, cancellationToken);
                return;
            }

            // Create new internal CTS for this fade operation
            fadeCts?.Dispose();
            fadeCts = new CancellationTokenSource();

            currentFadeOut = FadeAsync(0f, fadeCts.Token);

            try {
                await AwaitWithCancellation(currentFadeOut, cancellationToken);
                panel.Hide();
            }
            catch (OperationCanceledException) when (fadeCts.IsCancellationRequested) {
                // ForceCancel was called - cleanup already done
                throw;
            }
        }

        public override void ForceCancel() {
            if (!constructed) return;

            // Cancel any running fade
            fadeCts?.Cancel();
            fadeCts?.Dispose();
            fadeCts = null;

            // Reset to hidden state
            if (panel != null) {
                panel.SetOpacity(0);
                panel.Hide();
            }

            // Clear tracked tasks
            currentFadeIn = null;
            currentFadeOut = null;
        }

        /// <summary>
        /// Await a task while respecting caller's cancellation token.
        /// Caller's token only cancels their wait, not the underlying operation.
        /// </summary>
        private async Task AwaitWithCancellation(Task task, CancellationToken cancellationToken) {
            if (cancellationToken == default) {
                await task;
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            var completed = await Task.WhenAny(task, tcs.Task);

            if (completed == tcs.Task) {
                // Caller cancelled their wait
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Propagate any exception from the original task
            await task;
        }

        private async Task FadeAsync(float targetAlpha, CancellationToken cancellationToken) {
            float currentAlpha = panel.GetOpacity();

            while (!Mathf.Approximately(currentAlpha, targetAlpha)) {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                currentAlpha = Mathf.MoveTowards(
                    currentAlpha,
                    targetAlpha,
                    Time.unscaledDeltaTime * fadeSpeed
                );

                panel.SetOpacity(currentAlpha);
            }

            panel.SetOpacity(targetAlpha);
        }
    }
}
