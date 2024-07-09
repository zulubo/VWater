using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vertigo2
{
    /// <summary>
    /// Allow overriding default splash prefabs, sound fx, etc
    /// </summary>
    [CreateAssetMenu(fileName = "New Water Override", menuName = "Vertigo2/Water Override")]
    public class VWaterOverride : ScriptableObject
    {
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
        public GameObject splashPrefab_footstep;

        [Space(20)]

        public AudioClip au_wadeShallow;
        public AudioClip au_wadeMedium;
        public AudioClip au_wadeDeep;

        [Space(20)]
        public ComputeShader computeShader;

    }
}