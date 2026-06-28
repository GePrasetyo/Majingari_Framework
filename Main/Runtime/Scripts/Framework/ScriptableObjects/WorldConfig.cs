using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Majinfwork.World {
    [CreateAssetMenu(fileName = "Default World Config", menuName = "MFramework/Config Object/World Config")]
    public class WorldConfig : ScriptableObject {
        [Header("Scene Configuration")]
        [SerializeField] private WorldAssetConfig[] mapList = new WorldAssetConfig[0];
        [SerializeField] private AddressableSceneHandler[] levelStreamCollection = Array.Empty<AddressableSceneHandler>();

        [Header("Default Fallback")]
        [Tooltip("GameMode used when a scene has no specific GameMode configured. Addressable, loaded on demand.")]
        [SerializeField] private AssetReferenceT<GameModeManager> defaultGameMode;

        public Dictionary<string, WorldAssetConfig> MapConfigList = new Dictionary<string, WorldAssetConfig>();
        public Dictionary<string, AddressableSceneHandler> levelStreamDictionary = new Dictionary<string, AddressableSceneHandler>();

        [SerializeReference, ClassReference] private LoadingStreamer loadingHandler;

        [Tooltip("Optional additional loading streamers, addressable by key via " +
                 "LoadingStreamerRegistry.Get(\"<key>\"). A caller asking for an unknown " +
                 "key falls back to the default loadingHandler above.")]
        [SerializeField] private KeyedLoadingStreamer[] keyedLoadingStreamers = System.Array.Empty<KeyedLoadingStreamer>();

        public AssetReferenceT<GameModeManager> DefaultGameMode => defaultGameMode;

        public void SetupSceneConfiguration() {
            loadingHandler.Initialize();
            ServiceLocator.Register<LoadingStreamer>(loadingHandler);

            var registry = new LoadingStreamerRegistry(loadingHandler);
            if (keyedLoadingStreamers != null) {
                for (int i = 0; i < keyedLoadingStreamers.Length; i++) {
                    var entry = keyedLoadingStreamers[i];
                    if (entry.streamer == null) continue;
                    entry.streamer.Initialize();
                    registry.Register(entry.key, entry.streamer);
                }
            }
            ServiceLocator.Register<LoadingStreamerRegistry>(registry);

            MapConfigList.Clear();

            if (mapList.Length != 0) {
                for (int i = 0; i < mapList.Length; i++) {
                    MapConfigList[mapList[i].mapName] = mapList[i];
                }
            }

            levelStreamDictionary.Clear();

            if (levelStreamCollection.Length != 0) {
                for (int i = 0; i < levelStreamCollection.Length; i++) {
                    levelStreamCollection[i].status = SceneLoadStatus.Unloaded;
                    levelStreamDictionary[levelStreamCollection[i].sceneAddressable.AssetGUID] = levelStreamCollection[i];
                }
            }
        }
    }

    [Serializable]
    public class KeyedLoadingStreamer {
        [Tooltip("Lookup key — callers do LoadingStreamerRegistry.Get(key).")]
        public string key;
        [SerializeReference, ClassReference] public LoadingStreamer streamer;
    }

    [Serializable]
    public class WorldAssetConfig {
#if UNITY_EDITOR
        public SceneAsset Map;
#endif
        public string mapName;
        // Addressable — the GameModeManager and its dependency chain (HUD, pawn, input
        // prefabs) only load when this scene is activated. Was a direct SO reference,
        // which pulled every mini-game's assets into the boot-time Resources closure.
        public AssetReferenceT<GameModeManager> TheGameMode;
    }

    [Serializable]
    public class SceneReference {
#if UNITY_EDITOR
        public SceneAsset Map;
#endif
        public string mapName;
    }

    [Serializable]
    public class AddressableSceneHandler {
        public AssetReference sceneAddressable;
        internal AsyncOperationHandle<SceneInstance> streamHandler;
        internal Action<string> streamHandlerCompleted;
        [SerializeField] internal SceneLoadStatus status = SceneLoadStatus.Unloaded;

        public void UpdateHandler(AsyncOperationHandle<SceneInstance> obj) {
            if (obj.Status == AsyncOperationStatus.Failed) {
                goto Reset;
            }

            LightProbes.TetrahedralizeAsync();
            streamHandler = obj;
            streamHandlerCompleted?.Invoke(obj.Result.Scene.path);

            Reset:
            streamHandlerCompleted = null;
            status = obj.Result.Scene.isLoaded? SceneLoadStatus.Loaded:SceneLoadStatus.Unloaded;
        }
    }

    internal enum SceneLoadStatus { Unloaded, Loading, Loaded }
}
