using UnityEngine;

namespace LumenKart
{
    [CreateAssetMenu(menuName = "Lumen Circuit/Kart Tuning", fileName = "KartTuning")]
    public sealed class KartTuning : ScriptableObject
    {
        [Header("Speed")]
        [Min(1f)] public float maxForwardSpeed = 24f;
        [Min(1f)] public float maxReverseSpeed = 7.5f;
        [Min(0f)] public float acceleration = 31f;
        [Min(0f)] public float reverseAcceleration = 17f;
        [Min(0f)] public float brakePower = 43f;
        [Min(0f)] public float coastDrag = 2.2f;

        [Header("Steering")]
        [Min(0f)] public float steeringAcceleration = 15.5f;
        [Range(0.1f, 1f)] public float steeringAtTopSpeed = 0.52f;
        [Min(0f)] public float angularDamping = 4.2f;
        [Min(0f)] public float lateralGrip = 9.5f;
        [Min(0f)] public float airSteering = 1.7f;

        [Header("Grounding")]
        [Min(0.1f)] public float rideHeight = 0.62f;
        [Min(0.1f)] public float probeDistance = 1.35f;
        [Min(0.01f)] public float probeRadius = 0.28f;
        [Min(0f)] public float suspensionStrength = 92f;
        [Min(0f)] public float suspensionDamping = 12f;
        [Min(0f)] public float groundAlignment = 16f;
        [Min(0f)] public float customGravity = 23f;

        [Header("Drift")]
        [Min(0f)] public float minimumDriftSpeed = 7f;
        [Min(0f)] public float driftLateralGrip = 2.6f;
        [Min(0f)] public float driftSteeringMultiplier = 1.42f;
        [Min(0f)] public float driftHopImpulse = 2.7f;
        [Min(0f)] public float driftChargeRate = 1f;
        [Min(0f)] public float tierOneCharge = 0.75f;
        [Min(0f)] public float tierTwoCharge = 1.7f;
        [Min(0f)] public float tierThreeCharge = 2.8f;
        [Min(0f)] public float tierOneDuration = 0.55f;
        [Min(0f)] public float tierTwoDuration = 0.95f;
        [Min(0f)] public float tierThreeDuration = 1.35f;

        [Header("Boost")]
        [Min(0f)] public float boostAcceleration = 26f;
        [Min(1f)] public float defaultBoostSpeed = 31f;
        [Min(0f)] public float boostSteeringMultiplier = 0.9f;

        [Header("Recovery")]
        [Min(0f)] public float spinOutDuration = 0.85f;
        [Min(0f)] public float spinOutTorque = 16f;
        [Min(0f)] public float autoRespawnDelay = 2.25f;
    }
}
