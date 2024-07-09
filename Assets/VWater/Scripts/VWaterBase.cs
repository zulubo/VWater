using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Vertigo2
{
    /// <summary>
    /// Base class for water to allow support for other water systems like Crest
    /// </summary>
    public abstract class VWaterBase : MonoBehaviour
    {

        [Tooltip("How viscous should the physics for this water be")]
        public float viscosity = 1;

        public Vector3 globalFlow;

        [HideInInspector]
        public Bounds bounds;

        public bool allowTeleport = true;

        public bool allowBreatheUnderwater = false;

        [Tooltip("Allow overriding default splash prefabs, sound fx, etc")]
        public VWaterOverride waterOverride;

        public Rect bounds2D
        {
            get { return new Rect(new Vector2(bounds.min.x, bounds.min.z), new Vector2(bounds.size.x, bounds.size.z)); }
        }

        [HideInInspector]
        public List<VWaterBody> inWater = new List<VWaterBody>();


        protected virtual void OnEnable()
        {
            VWaterManager.activeWater.Add(this);
        }

        protected virtual void OnDisable()
        {
            for (int i = inWater.Count - 1; i >= 0; i--)
            {
                ForceBodyExit(inWater[i]);
            }
            VWaterManager.activeWater.Remove(this);
        }

        protected virtual void Update()
        {
            // check everything in water to see if it has left the water
            for (int i = inWater.Count - 1; i >= 0; i--)
            {
                if (inWater[i] == null)
                {
                    inWater.RemoveAt(i);
                }
                else
                {
                    inWater[i].StayInWater(this);

                    if (CheckForExitWater(inWater[i]))
                    {
                        ForceBodyExit(inWater[i]);
                    }
                }
            }
        }


        public abstract bool CheckForExitWater(VWaterBody b);

        public void ForceBodyExit(VWaterBody body)
        {
            if (inWater.Contains(body))
            {
                inWater.Remove(body);
                body.ExitWater(this);
            }
        }

        public void EnterWater(VWaterBody b)
        {
            if (b != null)
            {
                //start tracking this collider as an immersed body
                b.EnterWater(this);
                inWater.Add(b);

                if (!b.touchingWaterMask)
                {
                    Splash(b);
                }
            }
        }

        const float minSplashTime = 0.5f;
        void Splash(VWaterBody body)
        {
            if (Time.time - body.lastSplashedTime > minSplashTime)
            {
                body.lastSplashedTime = Time.time;
                VWaterManager.TriggerSplash(this, body);
            }
        }

        public void BulletHit(Vector3 position)
        {
            VWaterManager.instance.BulletSplash(this, position);
        }
    }
}