using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Vertigo2
{
    /// <summary>
    /// An object that should interact with water.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VWaterBody : MonoBehaviour
    {
        [Tooltip("Collider to use for water physics")]
        public Collider mainCollider;
        private float colliderSize;

        [Tooltip("Toggle water physics")]
        public bool applyWaterPhysics = true;
        [Tooltip("Density of this object. Densities >1 will sink, <1 will float.")]
        public float density = 1;
        [Tooltip("Strength of water drag")]
        public float dragMultiplier = 1f;
        [Tooltip("Strength of angular water drag")]
        public float angularDragMultiplier = 1f;
        [Range(0,1), Tooltip("Quadratic drag acts on the square of the velocity")]
        public float quadraticDragFactor = 1f;

        [Tooltip("Direction of center of buoyancy. Larger vectors will upright the object more strongly")]
        public Vector3 buoyantDirection = Vector3.up;

        [Space()]
        [Tooltip("Optionally adds ripples to the water wave simulation. You can leave this null for small objects")]
        public WaterDynamicFlowSource wake;
        public float wakeStrength = 2f;

        [Space()]

        [Tooltip("Rigidbodies have water physics applied automatically. Generic bodies can be used for animated objects that should splash when they enter water.")]
        public BodyTypes bodyType;
        public enum BodyTypes
        {
            Rigidbody,
            //Character,
            Generic
        }

        [Tooltip("certain things should ignore water masks, like boats, since they contain water masks")]
        public bool ignoreMasks;


        [HideInInspector]
        public VWaterBase currentWater;
        
        //timer to forget water
        int waterTimer;

        public delegate void WaterEvent(VWaterBase w);
        public event WaterEvent OnEnterWater;
        public event WaterEvent OnExitWater;

        // water masks, eg inside a boat, make it so we shouldn't interact with water
        [HideInInspector]
        public bool touchingWaterMask = false;

        Rigidbody rb;
        //Player.VertigoCharacterController character;
        VelocityEst velocityEstimator;

        /// <summary>
        /// a velocity to offset drag force, e.g. to stop swimming characters from being slowed down
        /// </summary>
        [HideInInspector] public Vector3 swimVelocity;
        public float submersion { get; private set; }

        [HideInInspector]
        public float waterHeight;
        [HideInInspector]
        public Vector3 waterNormal;
        [HideInInspector]
        public Vector3 waterVelocity;

#if CREST_WATER
        Crest.SampleHeightHelper _sampleHeightHelper = new Crest.SampleHeightHelper();
        Crest.SampleFlowHelper _sampleFlowHelper = new Crest.SampleFlowHelper();
#endif
        public Vector3 velocity
        {
            get
            {
                switch (bodyType)
                {
                    //case (BodyTypes.Character):
                    //    return character.motor.Velocity;

                    case (BodyTypes.Rigidbody):
                        return rb.velocity;

                    case (BodyTypes.Generic):
                        return velocityEstimator.GetVelocityEstimate();

                    default:
                        return Vector3.zero;
                }
            }
        }

        public Vector3 buoyantForce
        {
            get
            {
                return (transform.rotation * buoyantDirection) * Mathf.Clamp01(50 * submersion) * (1 - submersion);
            }
        }

        public bool inWater => m_inWater && !touchingWaterMask;
        private bool m_inWater;

        [HideInInspector]
        public float lastSplashedTime;

        void Awake()
        {
            if (bodyType == BodyTypes.Rigidbody)
            {
                rb = GetComponent<Rigidbody>();
            }
            //if (bodyType == BodyTypes.Character)
            //{
            //    character = GetComponent<Player.VertigoCharacterController>();
            //}
            if (bodyType == BodyTypes.Generic)
            {
                velocityEstimator = gameObject.AddComponent<VelocityEst>();
                velocityEstimator.BeginEstimatingVelocity();
            }

            if (mainCollider == null)
            {
                //collider not set manually, attempt to automatically choose
                mainCollider = GetComponent<Collider>();
                if (mainCollider == null) mainCollider = GetComponentInChildren<Collider>();
            }
            colliderSize = mainCollider.bounds.size.magnitude;
        }

        private void OnDisable()
        {
            m_inWater = false;
        }

        private void OnEnable()
        {
            m_inWater = false;

            if (bodyType == BodyTypes.Generic && velocityEstimator != null)
            {
                velocityEstimator.BeginEstimatingVelocity();
            }
        }

        private void Update()
        {
            if (inWater)
            {
                waterTimer--;
                if (waterTimer < 0)
                {
                    //water timer has run out, something has gone wrong and we're definitely out of the water.
                    currentWater.ForceBodyExit(this);
                }

                if (currentWater == null)
                {
                    m_inWater = false;
                    return;
                }

                submersion = CalculateSubmersion();

                if (wake != null && velocity.sqrMagnitude > 0.01f)
                {
                    if (submersion > 0.01f && submersion < 0.99f)
                    {
                        wake.enabled = true;
                        wake.amplitude = -Mathf.Clamp(velocity.magnitude, 0, 10) * 0.2f * wakeStrength;
                    }
                    else wake.enabled = false;
                }
                else if (wake != null) wake.enabled = false;
            }
            else if (wake != null) wake.enabled = false;
        }

        bool useGravity
        {
            get
            {
                switch (bodyType)
                {
                    case BodyTypes.Rigidbody:
                        return rb.useGravity;
                    default:
                        return true;
                }
            }
        }

        public void UpdateWaterInfo()
        {
            // update water info for VWater
            if (currentWater != null && currentWater is VWater)
            {
                waterHeight = ((VWater)currentWater).Position;
                waterNormal = Vector3.up;
                waterVelocity = currentWater.globalFlow;
            }
#if CREST_WATER
            // update water info for CrestWater
            CrestUpdate();
#endif
        }

        const float dragCoefficient = 1.5f;
        const float quadraticDragCoefficient = 4f;
        float CalculateDrag(float multiplier, float speed)
        {
            return 1 - Mathf.Exp(-(currentWater.viscosity * dragCoefficient * submersion * multiplier * Mathf.Lerp(1, speed / quadraticDragCoefficient, quadraticDragFactor)) * Time.fixedDeltaTime);
        }
        private void FixedUpdate()
        {
            UpdateWaterInfo();

            if (applyWaterPhysics)
            {
                if (inWater)
                {
                    if (useGravity)
                    {
                        float upforce = submersion / density;
                        AddVelocity(-Physics.gravity * upforce * Time.fixedDeltaTime);
                    }

                    if (bodyType == BodyTypes.Rigidbody)
                    {
                        // advanced drag
                        Vector3 dragPosition = rb.worldCenterOfMass;
                        Vector3 relativeVelocity = rb.velocity - waterVelocity - swimVelocity;
                        if(submersion < 1)
                        {
                            dragPosition.y = Mathf.Min(dragPosition.y, waterHeight);
                            relativeVelocity = rb.GetPointVelocity(dragPosition);
                        }
                        float drag = CalculateDrag(dragMultiplier, relativeVelocity.magnitude);
                        Vector3 dragForce = -relativeVelocity * drag;
                        rb.AddForceAtPosition(dragForce, dragPosition, ForceMode.VelocityChange);

                        float angDrag = CalculateDrag(angularDragMultiplier, rb.angularVelocity.magnitude);
                        rb.angularVelocity -= rb.angularVelocity * angDrag;

                        // buoyant torque
                        if (buoyantDirection != Vector3.zero)
                        {
                            float buoy = 5f;
                            rb.AddForce(-waterNormal * buoy, ForceMode.Acceleration);
                            rb.AddForceAtPosition(waterNormal * buoy, transform.position + buoyantForce, ForceMode.Acceleration);
                        }
                    }
                    else
                    {
                        // basic drag
                        Vector3 relativeVelocity = velocity - waterVelocity - swimVelocity;
                        AddVelocity(-relativeVelocity * CalculateDrag(dragMultiplier, relativeVelocity.magnitude));
                    }
                }
            }
        }

#if CREST_WATER
        void CrestUpdate()
        {
            UnityEngine.Profiling.Profiler.BeginSample("WaterBody.CrestUpdate");

            if (!CrestWater.exists)
            {
                UnityEngine.Profiling.Profiler.EndSample();
                return;
            }

            _sampleHeightHelper.Init(transform.position, colliderSize, true);
            _sampleHeightHelper.Sample(out Vector3 disp, out var normal, out var waterSurfaceVel);

            {
                _sampleFlowHelper.Init(transform.position, colliderSize);

                _sampleFlowHelper.Sample(out var surfaceFlow);
                waterSurfaceVel += new Vector3(surfaceFlow.x, 0, surfaceFlow.y);
            }

            float crestWaterHeight = disp.y + Crest.OceanRenderer.Instance.SeaLevel;
            float bottomDepth = crestWaterHeight - mainCollider.bounds.min.y;

            bool _inWater = bottomDepth > 0f;

            if(_inWater && (currentWater == null || currentWater != CrestWater.instance))
            {
                CrestWater.instance.EnterWater(this);
            }

            if (currentWater != null && currentWater == CrestWater.instance)
            {
                waterHeight = crestWaterHeight;
                waterNormal = normal;
                waterVelocity = waterSurfaceVel;
                float velDepth = (currentWater as CrestWater).surfaceVelocityDepth;
                if (velDepth > 0)
                {
                    float velocityFalloff = Mathf.InverseLerp(waterHeight - velDepth, waterHeight, mainCollider.bounds.max.y);
                    waterVelocity *= velocityFalloff;
                }
            }

            UnityEngine.Profiling.Profiler.EndSample();
        }
#endif


        public Vector3 VectorPow(Vector3 vec, float pow)
        {
            float mag = vec.magnitude;
            return vec.normalized * Mathf.Pow(mag, pow);
        }

        float CalculateSubmersion()
        {
            if (currentWater == null) return 0; //not in water

            Vector3 com = mainCollider.bounds.center;
            Vector3 ext = mainCollider.bounds.extents;
            //average extents to get rough radius
            float radius = (Mathf.Abs(ext.x) + Mathf.Abs(ext.y) + Mathf.Abs(ext.z)) / 3;
            //approximate sphere volume submersion
            float submersion = Mathf.SmoothStep(0, 1, (waterHeight - (com.y - ext.y)) / (2 * ext.y));

            return submersion;
        }

        const int waterTimerVal = 10; // how long to forget water


        private void OnTriggerEnter(Collider other)
        {
            VWaterBase w = other.GetComponent<VWaterBase>();
            if (w != null)
            {
                w.EnterWater(this);
            }
        }

        public void EnterWater(VWaterBase water)
        {
            if (currentWater != water)
            {
                if (currentWater != null) OnExitWater?.Invoke(currentWater);
                OnEnterWater?.Invoke(water);
            }

            m_inWater = true;
            currentWater = water;
            waterTimer = waterTimerVal;
        }

        public void StayInWater(VWaterBase water)
        {
            m_inWater = true;
            currentWater = water;
            waterTimer = waterTimerVal;
        }

        public void ExitWater(VWaterBase water)
        {
            if(currentWater != null)
            {
                OnExitWater?.Invoke(water);
            }

            currentWater = null;
            m_inWater = false;
        }

        public void EnterWaterMask()
        {
            if (!ignoreMasks) touchingWaterMask = true;
        }
        public void ExitWaterMask()
        {
            if (!ignoreMasks) touchingWaterMask = false;
        }


        private void AddVelocity(Vector3 vel)
        {
            /*if (bodyType == BodyTypes.Character)
            {
                // we're attached to a character controller
                // no water physics if standing on something
                if (!character.standingOnSurface)
                {
                    character.velocity += vel;
                }
            }*/

            if (bodyType == BodyTypes.Rigidbody)
            {
                rb.velocity += vel;
            }
        }

    }
}