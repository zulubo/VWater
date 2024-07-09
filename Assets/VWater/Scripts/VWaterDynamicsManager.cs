using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vertigo2 {
    public class VWaterDynamicsManager : MonoBehaviour
    {

        public static VWaterDynamicsManager instance;
        public static RenderTexture waterBuffer;
        private static RenderTexture sourceBuffer;
        private static RenderTexture oldBuffer;

        [Header("Water Compute Shader")]

        public ComputeShader compute;
        private ComputeShader defaultCompute;
        private int waterKernel;
        private int offsetKernel;

        public int bufferSize = 512;
        const int THREAD_COUNT = 32;

        public float simFramerate = 60;


        [Header("Water Universe")]

        public float universeSize = 10; // size of the water universe
        [HideInInspector]
        public Vector2 universePos;    // position of the water universe

        public bool IsPositionInUniverse(Vector3 pos)
        {
            float bufferSize = universeSize * 1.25f;
            return (pos.x > universePos.x - bufferSize && pos.x < universePos.x + bufferSize
                && pos.z > universePos.y - bufferSize && pos.z < universePos.y + bufferSize);
        }

        public float followCameraGridSize = 1f;
        private Vector2Int universeGridPos;

        public Texture2D flowMap;
        public float flowMapScale = 10;


        public struct FlowSource
        {
            public Vector2 position;
            public float radius;
            public float amplitude;
            public int priority;
            public bool inUse;
            public float distance;

            public FlowSource(Vector3 worldPosition, float radius, float amplitude, int priority = 0)
            {
                position = new Vector2(worldPosition.x, worldPosition.z);
                this.radius = radius;
                this.amplitude = amplitude;
                this.priority = priority;
                inUse = true;
                distance = 0;
                distance = CalcDistance();
            }

            public Vector3 worldPosition
            {
                get
                {
                    return new Vector3(position.x, 0, position.y);
                }
                set
                {
                    position = new Vector2(value.x, value.z);
                }
            }

            public void Reset()
            {
                position = Vector2.zero;
                radius = 0;
                amplitude = 0;
                priority = -100;
                inUse = false;
            }

            float CalcDistance()
            {
                if (MainCamera == null) return 0;
                else return (MainCamera.transform.position - worldPosition).sqrMagnitude;
            }
        }

        public const int FLOW_SOURCE_MAX = 4;
        public FlowSource[] flowSources = new FlowSource[FLOW_SOURCE_MAX];



        //make sure this happens first
        private void Awake()
        {
            instance = this;

            defaultCompute = compute;

            SetupBuffer();
            SetUpCompute();

            StartCoroutine(SimCoroutine());
            StartCoroutine(CullingCoroutine());
        }


        void SetupBuffer()
        {
            // create buffer rendertextures
            waterBuffer = new RenderTexture(bufferSize, bufferSize, 24);
            waterBuffer.enableRandomWrite = true;
            waterBuffer.format = RenderTextureFormat.ARGBHalf;
            waterBuffer.Create();

            sourceBuffer = new RenderTexture(bufferSize, bufferSize, 24);
            sourceBuffer.enableRandomWrite = true;
            sourceBuffer.format = RenderTextureFormat.ARGBHalf;
            sourceBuffer.Create();

            oldBuffer = new RenderTexture(bufferSize, bufferSize, 24);
            oldBuffer.enableRandomWrite = true;
            oldBuffer.format = RenderTextureFormat.ARGBHalf;
            oldBuffer.Create();
        }

        void SetUpCompute()
        {
            // set constant shader vars
            compute.SetInt("BUFFER_SIZE", bufferSize);
            compute.SetFloat("worldSize", universeSize);
            compute.SetFloat("flowScale", flowMapScale);

            // generate kernel IDs
            waterKernel = compute.FindKernel("CSWater");
            offsetKernel = compute.FindKernel("CSOffsetBuffers");

            compute.SetTexture(waterKernel, "WaterBuffer", waterBuffer);
            compute.SetTexture(waterKernel, "SourceBuffer", sourceBuffer);
            compute.SetTexture(waterKernel, "OldBuffer", oldBuffer);

            compute.SetTexture(waterKernel, "_FlowMap", flowMap);
        }


        IEnumerator SimCoroutine()
        {
            while (true)
            {
                //while (GameManager.options.graphics_gpuWaterDynamics == 0) 
                //    yield return new WaitForSeconds(1f); // idle while water dynamics are turned off

                float deltaTime = 1f / simFramerate;
                yield return new WaitForSeconds(deltaTime);
                yield return new WaitForEndOfFrame();
                UpdateSimulation(deltaTime);
            }
        }

        static Camera MainCamera
        {
            get
            {
                if(_mainCamera == null || _mainCamera.gameObject == null || !_mainCamera.isActiveAndEnabled)
                {
                    _mainCamera = Camera.main;
                }
                return _mainCamera;
            }
        }
        static Camera _mainCamera;

        void UpdateSimulation(float deltaTime)
        {
            if (MainCamera == null) return;
            Vector3 camPos = MainCamera.transform.position;
            Vector2Int camGridPos = new Vector2Int(Mathf.RoundToInt(camPos.x / followCameraGridSize),
                                                    Mathf.RoundToInt(camPos.z / followCameraGridSize));

            if (camGridPos != universeGridPos)
            {
                universeGridPos = camGridPos;
                Vector2 newUniversePos = ((Vector2)universeGridPos * followCameraGridSize);
                MoveUniverse(newUniversePos);
            }

            if (nearAnyWater)
            {
                UpdateComputeShader(deltaTime);
            }
        }

        bool nearAnyWater = false;
        IEnumerator CullingCoroutine()
        {
            VWaterBase nearestWater = null;
            float nearestDist = float.MaxValue;

            while (true)
            {
                //while (GameManager.options.graphics_gpuWaterDynamics == 0)
                //    yield return new WaitForSeconds(1f); // idle while water dynamics are turned off

                nearestWater = null;
                nearestDist = float.MaxValue;

                bool anyWater = false;
                for (int i = 0; i < VWaterManager.activeWater.Count; i++)
                {
                    if (VWaterManager.activeWater[i] != null && VWaterManager.activeWater[i] is VWater && ((VWater)VWaterManager.activeWater[i]).linkedRenderer != null)
                    {
                        bool near = IsNearWater(VWaterManager.activeWater[i]);
                        ((VWater)VWaterManager.activeWater[i]).linkedRenderer.cullDynamics = !near;
                        if (near) anyWater = true;

                        if (MainCamera != null)
                        {
                            // find nearest water by comparing distance to surface bounds
                            Vector3 clampPos = MainCamera.transform.position;
                            Rect b = VWaterManager.activeWater[i].bounds2D;
                            clampPos = new Vector3(Mathf.Clamp(clampPos.x, b.xMin, b.xMax), VWaterManager.activeWater[i].transform.position.y, Mathf.Clamp(clampPos.z, b.yMin, b.yMax));
                            float dist = (MainCamera.transform.position - clampPos).sqrMagnitude;
                            if(dist < nearestDist)
                            {
                                nearestWater = VWaterManager.activeWater[i];
                                nearestDist = dist;
                            }
                        }
                    }
                    yield return null;
                }

                if(nearestWater != null)
                {
                    // allow using custom compute shader per-water
                    if(nearestWater.waterOverride != null && nearestWater.waterOverride.computeShader != null)
                    {
                        if (compute != nearestWater.waterOverride.computeShader) 
                        {
                            compute = nearestWater.waterOverride.computeShader;
                            SetUpCompute();
                        }
                    }
                    else
                    {
                        if (compute != defaultCompute)
                        {
                            compute = defaultCompute;
                            SetUpCompute();
                        }
                    }
                }

                nearAnyWater = anyWater;

                yield return null;
            }
        }

        public void AddFlow(Vector3 position, float radius, float amplitude, int priority = 0)
        {
            OnFlowAdded?.Invoke(position, radius, amplitude, priority);

            if (!nearAnyWater) return;

            if (!IsPositionInUniverse(position)) return; // don't add this flow source if it's outside universe
            position.y = 0;

            // find a free flow source to add to.
            for (int i = 0; i < FLOW_SOURCE_MAX; i++)
            {
                if (!flowSources[i].inUse)
                {
                    flowSources[i] = new FlowSource(position, radius, amplitude, priority);
                    return;
                }
            }

            // override existing flow sources if higher priority
            for (int i = 0; i < FLOW_SOURCE_MAX; i++)
            {
                float dist = (CameraPos() - position).sqrMagnitude;
                if (!flowSources[i].inUse
                    || (priority > flowSources[i].priority
                    || (priority == flowSources[i].priority && dist < flowSources[i].distance)))
                {
                    flowSources[i] = new FlowSource(position, radius, amplitude, priority);
                    return;
                }
            }
        }
        public delegate void FlowEvent(Vector3 position, float radius, float amplitude, int priority = 0);
        public static event FlowEvent OnFlowAdded;

        Vector3 CameraPos()
        {
            if (MainCamera != null) return MainCamera.transform.position;
            else return Vector3.zero;
        }

        // buffers to hold flow source data before being passed to shader
        Vector4[] flowSourcePosBuffer = new Vector4[FLOW_SOURCE_MAX];
        float[] flowSourceRadiusBuffer = new float[FLOW_SOURCE_MAX * 4]; // these are x4 to align to HLSL rules
        float[] flowSourceAmpBuffer = new float[FLOW_SOURCE_MAX * 4];

        // shader var hashes
        private int _fsPos = Shader.PropertyToID("fsPos");
        private int _fsRadius = Shader.PropertyToID("fsRadius");
        private int _fsAmp = Shader.PropertyToID("fsAmp");

        private void Update()
        {
            // copy from flow source struct to arrays
            for (int i = 0; i < FLOW_SOURCE_MAX; i++)
            {
                flowSourcePosBuffer[i] = flowSources[i].position;
                flowSourceRadiusBuffer[i * 4] = flowSources[i].radius;
                flowSourceAmpBuffer[i * 4] = flowSources[i].amplitude;

                flowSources[i].Reset(); // clear for next frame
            }
        }

        private void UpdateComputeShader(float deltaTime)
        {
            // send flow data to shader
            compute.SetVectorArray(_fsPos, flowSourcePosBuffer);
            compute.SetFloats(_fsRadius, flowSourceRadiusBuffer);
            compute.SetFloats(_fsAmp, flowSourceAmpBuffer);

            // set input buffer
            Graphics.Blit(waterBuffer, sourceBuffer);

            compute.SetVector("worldPos", universePos);
            compute.SetFloat("deltaTime", deltaTime);
            compute.SetFloat("time", Time.time);
            compute.Dispatch(waterKernel, bufferSize / THREAD_COUNT, bufferSize / THREAD_COUNT, 1);

            // store the previous frame's buffer
            Graphics.Blit(sourceBuffer, oldBuffer);
        }


        private void MoveUniverse(Vector2 newPosition)
        {
            Vector2 oldPosition = universePos;
            Vector2 delta = newPosition - oldPosition;

            OffsetBuffers(delta);

            universePos = newPosition;
        }


        private void OffsetBuffers(Vector2 offset)
        {
            compute.SetVector("offsetVector", offset);


            // ======== Previous Frame =========

            // store the oldBuffer so we can offset it
            Graphics.Blit(oldBuffer, sourceBuffer);

            // tell the shader to offset the oldBuffer using sourceBuffer as a source
            compute.SetTexture(offsetKernel, "SourceBuffer", sourceBuffer);
            compute.SetTexture(offsetKernel, "WaterBuffer", oldBuffer);

            compute.Dispatch(offsetKernel, bufferSize / THREAD_COUNT, bufferSize / THREAD_COUNT, 1);



            // ======== Main =========

            // store the waterBuffer so we can offset it
            Graphics.Blit(waterBuffer, sourceBuffer);

            // tell the shader to offset the waterBuffer using sourceBuffer as a source
            compute.SetTexture(offsetKernel, "SourceBuffer", sourceBuffer);
            compute.SetTexture(offsetKernel, "WaterBuffer", waterBuffer);

            compute.Dispatch(offsetKernel, bufferSize / THREAD_COUNT, bufferSize / THREAD_COUNT, 1);
        }

        public void ClearBuffer(RenderTexture renderTexture)
        {
            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = rt;
        }

        bool IsNearWater(VWaterBase w)
        {
            Rect universeRect = new Rect(universePos - Vector2.one * universeSize / 2, Vector2.one * universeSize);

            return universeRect.Overlaps(w.bounds2D);
        }
    }
}