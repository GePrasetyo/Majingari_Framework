using System;
using UnityEngine;

namespace Majinfwork.World {
    [Serializable]
    public abstract class PawnProvider {
        public abstract PlayerPawn GetPawn(PlayerState state, Vector3 position, Quaternion rotation);
        public abstract void ReleasePawn(PlayerPawn pawn);
    }
}
