using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vertigo2 
{
    public class VWaterManager : MonoBehaviour
    {
        public static VWaterManager instance;

        public static List<VWaterBase> activeWater = new List<VWaterBase>();

        [Header("References")]

        public VWaterDynamicsManager dynamicsManager;

        [Space(10)]

        // small - smaller than a person
        public GameObject splashPrefab_Small_Light;
        public GameObject splashPrefab_Small_Heavy;

        [Space(10)]

        // medium - person sized
        public GameObject splashPrefab_Med_Light;
        public GameObject splashPrefab_Med_Heavy;

        [Space(10)]

        // large - larger than a person
        public GameObject splashPrefab_Large_Light;
        public GameObject splashPrefab_Large_Heavy;

        [Space(10)]

        public GameObject splashPrefab_bullet;

        [Space(10)]

        public GameObject splashPrefab_footstep;

        [Header("Options")]

        [Tooltip("Speed at which objects will cause a splash")]
        public float splashVelocity = 1f;
        [Tooltip("Speed at which objects will cause a heavier splash")]
        public float heavySplashVelocity = 3f;


        private void Start()
        {
            instance = this;
        }


        public static void TriggerSplash(VWaterBase water, VWaterBody body)
        {
            instance.Splash(water, body);
        }

        public void Splash(VWaterBase water, VWaterBody body)
        {
            if (Mathf.Abs(body.velocity.y) > splashVelocity)
            {
                var wov = water != null ? water.waterOverride : null;
                bool heavy = Mathf.Abs(body.velocity.y) > heavySplashVelocity;

                GameObject splashPrefab;

                if(body.mainCollider.bounds.size.magnitude > 3.5f)
                {
                    //is a large object, spawn large splash
                    if (heavy)
                    {
                        splashPrefab = splashPrefab_Large_Heavy;
                        if (wov != null && wov.splashPrefab_Large_Heavy != null) splashPrefab = wov.splashPrefab_Large_Heavy; 
                    }
                    else
                    {
                        splashPrefab = splashPrefab_Large_Light;
                        if (wov != null && wov.splashPrefab_Large_Light != null) splashPrefab = wov.splashPrefab_Large_Light;
                    }
                }
                else if (body.mainCollider.bounds.size.magnitude < 1f)
                {
                    //is a small object, spawn small splash
                    if (heavy)
                    {
                        splashPrefab = splashPrefab_Small_Heavy;
                        if (wov != null && wov.splashPrefab_Small_Heavy != null) splashPrefab = wov.splashPrefab_Small_Heavy;
                    }
                    else
                    {
                        splashPrefab = splashPrefab_Small_Light;
                        if (wov != null && wov.splashPrefab_Small_Light != null) splashPrefab = wov.splashPrefab_Small_Light;
                    }
                }
                else
                {
                    //is a small object, spawn small splash
                    if (heavy)
                    {
                        splashPrefab = splashPrefab_Med_Heavy;
                        if (wov != null && wov.splashPrefab_Med_Heavy != null) splashPrefab = wov.splashPrefab_Med_Heavy;
                    }
                    else
                    {
                        splashPrefab = splashPrefab_Med_Light;
                        if (wov != null && wov.splashPrefab_Med_Light != null) splashPrefab = wov.splashPrefab_Med_Light;
                    }
                }

                if (splashPrefab == null) splashPrefab = splashPrefab_Med_Light; // default to med if prefabs missing

                Vector3 spawnPos = body.mainCollider.bounds.center;
                body.UpdateWaterInfo();
                spawnPos.y = body.waterHeight + 0.01f;

                Instantiate(splashPrefab, spawnPos, splashPrefab.transform.rotation);
            }
        }

        public void BulletSplash(VWaterBase water, Vector3 position)
        {
            var splashPrefab = (water != null && water.waterOverride != null && water.waterOverride.splashPrefab_bullet != null) ? water.waterOverride.splashPrefab_bullet : splashPrefab_bullet;
            Instantiate(splashPrefab, position, splashPrefab.transform.rotation);
        }

        public void FootstepSplash(VWaterBase water, Vector3 position)
        {
            var splashPrefab = (water != null && water.waterOverride != null && water.waterOverride.splashPrefab_footstep != null) ? water.waterOverride.splashPrefab_footstep : splashPrefab_footstep;
            Instantiate(splashPrefab, position, splashPrefab.transform.rotation);
        }
    }
}
