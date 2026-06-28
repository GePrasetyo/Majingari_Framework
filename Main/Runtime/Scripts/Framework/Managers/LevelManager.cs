using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Majinfwork.World {
    /// <summary>
    /// Manages level loading and GameMode transitions.
    /// GameModes are now loaded via Addressables so each mini-game's closure
    /// (HUD prefab, pawn prefab, input prefab) stays out of the boot Resources archive
    /// and only pulls into memory when its scene activates.
    /// </summary>
    public class LevelManager {
        private readonly WorldConfig worldConfig;
        private GameModeManager currentGameMode;
        private GameModeManager currentGameModeTemplate;
        private AsyncOperationHandle<GameModeManager> currentGameModeHandle;
        private bool hasCurrentHandle;

        public GameModeManager CurrentGameMode => currentGameMode;
        public bool IsLoading { get; private set; }
        public string CurrentSceneName { get; private set; }

        public LevelManager(WorldConfig worldConfig) {
            this.worldConfig = worldConfig;
        }

        /// <summary>
        /// Loads a level asynchronously with full GameMode management.
        /// </summary>
        public async Task LoadLevelAsync(string sceneName, CancellationToken cancellationToken = default) {
            if (IsLoading) {
                Debug.LogWarning("[LevelManager] Already loading a level. Ignoring request.");
                return;
            }

            IsLoading = true;

            try {
                // Resolve the Addressable GameMode first so we can error out early
                // without having already swapped scenes.
                var resolve = ResolveGameModeAsync(sceneName);
                var (targetTemplate, targetHandle) = await resolve;

                DeactivateCurrentGameMode();

                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name == sceneName && activeScene.isLoaded) {
                    Debug.Log($"[LevelManager] Scene already loaded: {sceneName}");
                }
                else {
                    await LoadSceneInternalAsync(sceneName, cancellationToken);
                }

                ActivateGameMode(targetTemplate, targetHandle);

                CurrentSceneName = sceneName;
                Debug.Log($"[LevelManager] Level ready: {sceneName}");
            }
            catch (OperationCanceledException) {
                Debug.Log($"[LevelManager] Level load cancelled: {sceneName}");
                throw;
            }
            catch (Exception e) {
                Debug.LogError($"[LevelManager] Failed to load level {sceneName}: {e.Message}");
                throw;
            }
            finally {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Resolves the GameMode asset for a scene. Returns both the loaded asset and its
        /// Addressables handle so the caller can release it on deactivation. Handle is
        /// <c>default</c> (unused) when no Addressable applies.
        /// </summary>
        private async Task<(GameModeManager template, AsyncOperationHandle<GameModeManager> handle)>
            ResolveGameModeAsync(string sceneName) {

            if (worldConfig == null) {
                Debug.LogError("[LevelManager] WorldConfig is null!");
                return (null, default);
            }

            AssetReferenceT<GameModeManager> reference = null;

            if (worldConfig.MapConfigList.TryGetValue(sceneName, out var mapConfig)
                && mapConfig.TheGameMode != null && mapConfig.TheGameMode.RuntimeKeyIsValid()) {
                reference = mapConfig.TheGameMode;
            }
            else if (worldConfig.DefaultGameMode != null && worldConfig.DefaultGameMode.RuntimeKeyIsValid()) {
                Debug.LogWarning($"[LevelManager] Scene '{sceneName}' has no GameMode; using default.");
                reference = worldConfig.DefaultGameMode;
            }

            if (reference == null) {
                Debug.LogWarning($"[LevelManager] No GameMode (default or specific) configured for scene: {sceneName}");
                return (null, default);
            }

            var handle = reference.LoadAssetAsync<GameModeManager>();
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded) {
                Debug.LogError($"[LevelManager] Failed to load Addressable GameMode for scene '{sceneName}'.");
                Addressables.Release(handle);
                return (null, default);
            }

            return (handle.Result, handle);
        }

        /// <summary>
        /// Deactivates the current GameMode and releases its Addressables handle.
        /// </summary>
        private void DeactivateCurrentGameMode() {
            if (currentGameMode != null) {
                Debug.Log($"[LevelManager] Deactivating GameMode: {currentGameMode.name}");
                currentGameMode.OnDeactive();
                UnityEngine.Object.Destroy(currentGameMode);
                currentGameMode = null;
            }

            if (hasCurrentHandle) {
                Addressables.Release(currentGameModeHandle);
                currentGameModeHandle = default;
                hasCurrentHandle = false;
            }

            currentGameModeTemplate = null;
        }

        /// <summary>
        /// Activates a new GameMode. Claim ownership of the Addressables handle so the
        /// asset stays loaded for the lifetime of the active GameMode and is released
        /// cleanly on the next deactivation.
        /// </summary>
        private void ActivateGameMode(GameModeManager targetTemplate,
            AsyncOperationHandle<GameModeManager> targetHandle) {

            if (targetTemplate != null) {
                Debug.Log($"[LevelManager] Activating GameMode: {targetTemplate.name}");
                currentGameMode = UnityEngine.Object.Instantiate(targetTemplate);
                currentGameModeTemplate = targetTemplate;
                currentGameModeHandle = targetHandle;
                hasCurrentHandle = targetHandle.IsValid();
                currentGameMode.OnActive();
            }
            else {
                Debug.LogWarning("[LevelManager] No GameMode for this level.");
                // Nothing to own — release the (possibly default) handle if it's valid.
                if (targetHandle.IsValid()) Addressables.Release(targetHandle);
            }
        }

        private async Task LoadSceneInternalAsync(string sceneName, CancellationToken cancellationToken) {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (operation == null) {
                throw new Exception($"Failed to start loading scene: {sceneName}");
            }

            while (!operation.isDone) {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        /// <summary>
        /// Shuts down the LevelManager and cleans up the current GameMode + its Addressables handle.
        /// </summary>
        public void Shutdown() {
            DeactivateCurrentGameMode();
        }
    }
}
