/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vertigo2
{
    /// <summary>
    /// A body of water using the Crest Ocean System. An infinite GPU simulated ocean.
    /// Uncomment this script if you're using Crest and want to integrate it
    /// </summary>
    public class CrestWater : VWaterBase
    {
        public static CrestWater instance;
        public static bool exists => instance != null;

        public float surfaceVelocityDepth = 10;

        [Tooltip("only need to set this if using floating origin")]
        public Crest.ShapeGerstnerBatched[] waves;

        protected override void OnEnable()
        {
            base.OnEnable();
            instance = this;
            FloatingOriginManager.OnWorldOffset += FloatingOriginManager_OnWorldOffset;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            instance = null;
            FloatingOriginManager.OnWorldOffset -= FloatingOriginManager_OnWorldOffset;
        }

        public override bool CheckForExitWater(VWaterBody b)
        {
            return b.mainCollider.bounds.min.y > b.waterHeight;
        }

        private void FloatingOriginManager_OnWorldOffset(Vector3 offset)
        {
            for (int i = 0; i < waves.Length; i++)
            {
                if (waves[i] != null) waves[i].SetOrigin(-offset);
            }
        }
    }
}
*/