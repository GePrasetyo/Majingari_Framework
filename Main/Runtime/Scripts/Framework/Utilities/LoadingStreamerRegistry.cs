using System.Collections.Generic;

namespace Majinfwork.World {
    /// <summary>
    /// Service that holds multiple <see cref="LoadingStreamer"/> instances keyed by name
    /// plus a default fallback. Registered by <see cref="WorldConfig.SetupSceneConfiguration"/>
    /// so callers can pick a transition style per load (e.g. a "minigame" sliding transition
    /// vs. a plain "default" fade). If a requested key isn't registered, the default is returned.
    /// </summary>
    public class LoadingStreamerRegistry {
        private readonly Dictionary<string, LoadingStreamer> streamers = new();
        private readonly LoadingStreamer defaultStreamer;

        public LoadingStreamerRegistry(LoadingStreamer defaultStreamer) {
            this.defaultStreamer = defaultStreamer;
        }

        public LoadingStreamer Default => defaultStreamer;

        internal void Register(string key, LoadingStreamer streamer) {
            if (string.IsNullOrEmpty(key) || streamer == null) return;
            streamers[key] = streamer;
        }

        /// <summary>Returns the streamer for <paramref name="key"/>, or the default if the key isn't registered.</summary>
        public LoadingStreamer Get(string key) {
            if (!string.IsNullOrEmpty(key) && streamers.TryGetValue(key, out var s) && s != null)
                return s;
            return defaultStreamer;
        }
    }
}
