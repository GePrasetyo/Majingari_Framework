using System;

namespace Majinfwork {
    [Serializable]
    public abstract class CameraHandler {
        public abstract void Construct();
        public abstract void Deconstruct();
    }
}