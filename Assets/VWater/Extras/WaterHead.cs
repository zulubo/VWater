using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Vertigo2.Player
{
    /// <summary>
    /// The following is the script I use for submerging fx and sounds when you put your head underwater in vertigo 2. 
    /// It will probably require some modification to use for other games
    /// </summary>
    public class WaterHead : MonoBehaviour
    {
        public Transform origin;

        float subdep;

        public ParticleSystem bubbler;

        public AudioMixer audiom;
        float muffle;

        public VWaterBody waterBody;

        [HideInInspector]
        public bool submerged { get; private set; }
        bool wassub;

        [Header("Drowning")]
        public bool drowningEnabled = true;
        public AudioSource drownAudio;
        public float drownTime = 15;
        public AudioClip au_drown;
        public ParticleSystem fx_drown;
        public float breatheTime = 5f;
        public AudioClip au_breathe;
        public ParticleSystem fx_breathe;
        public AudioClip au_gasp;
        private float drownTimer = 0;
        private float breathTimer = 0;
        [HideInInspector] public bool drowning = false;

        [Header("SubmersionAudio")]
        public AudioSource submergeAudio;
        public AudioClip au_submerge;
        public AudioClip au_emerge;
        const float maxSubmergeVelocity = 1f;


        private void OnDisable()
        {
            // reset audio muffling when disabled
            if(audiom != null) audiom.SetFloat("Muffler", 22000);
        }
        void Update()
        {
            if (waterBody.inWater)
            {
                subdep = (waterBody.waterHeight) - Camera.main.nearClipPlane;
            }
            submerged = waterBody.inWater && origin.position.y < subdep;
            if (!wassub && submerged)
            {
                EnterWater();
            }
            if(wassub && !submerged)
            {
                ExitWater();
            }

            muffle = Mathf.MoveTowards(muffle, submerged ? 1 : 0, Time.deltaTime * 8);
            audiom.SetFloat("Muffler", Mathf.Lerp(22000, 500, Mathf.Pow(muffle, 0.2f)));


            // drowning
            if (drowningEnabled)
            {
                if (submerged && !waterBody.currentWater.allowBreatheUnderwater)
                {
                    drownTimer += Time.deltaTime;
                    breathTimer += Time.deltaTime;

                    if (drownTimer > drownTime)
                    {
                        drowning = true;
                        drownTimer = 0;
                        fx_drown.Play();
                        drownAudio.clip = au_drown;
                        drownAudio.Play();
                    }

                    if (breathTimer > breatheTime && !drowning)
                    {
                        breathTimer = 0;
                        fx_breathe.Play();
                        drownAudio.clip = au_breathe;
                        drownAudio.Play();
                    }


                    if (drowning)
                    {
                        //VertigoPlayer.instance.Hit(new HitInfo(100f * Time.deltaTime / 4f, DamageType.Drowning));
                    }
                }
                else
                {
                    drowning = false;
                    drownTimer = 0;
                    breathTimer = 0;
                }
            }



            wassub = submerged;
            //VertigoCharacterController.player.underWater = submerged;
        }

        void EnterWater()
        {
            float vel = waterBody.velocity.y; //VertigoCharacterController.instance.velocity.y;
            if (vel < -0.3f)
            {
                bubbler.transform.position = new Vector3(Camera.main.transform.position.x, waterBody.waterHeight, Camera.main.transform.position.z);
                bubbler.transform.parent = waterBody.currentWater.transform;
                ParticleSystem.MainModule m = bubbler.main;
                m.startSpeedMultiplier = -vel * 2;
                m.startLifetimeMultiplier = Mathf.Sqrt(-vel) + 2;

                bubbler.Emit(Mathf.RoundToInt(Mathf.Lerp(10, 200, -vel / 20)));
            }

            if(submergeAudio != null)
            {
                float subV = Mathf.Clamp01(-waterBody.velocity.y / maxSubmergeVelocity);
                if (subV > 0.2f)
                {
                    PlaySound(submergeAudio, au_submerge, subV);
                }
            }
        }

        void ExitWater()
        {
            if (drowningEnabled)
            {
                fx_drown.Stop();
                fx_breathe.Stop();
                drownAudio.Stop();
                if (drowning)
                {
                    PlaySound(drownAudio, au_gasp);
                }
            }

            if (submergeAudio != null)
            {
                float subV = Mathf.Clamp01(waterBody.velocity.y / maxSubmergeVelocity);
                if (subV > 0.2f)
                {
                    PlaySound(submergeAudio, au_emerge, subV);
                }
            }
        }

        void PlaySound(AudioSource source, AudioClip clip, float volume = 1)
        {
            source.volume = volume;
            source.clip = clip;
            source.Play();
        }
    }
}