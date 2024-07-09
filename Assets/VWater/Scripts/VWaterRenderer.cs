using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vertigo2
{
    [ExecuteInEditMode]
    public class VWaterRenderer : MonoBehaviour
    {
        [Header("References")]

        [Tooltip("Material to draw over camera when submerged")]
        public Material overlayMaterial;
        [Tooltip("Water to render")]
        public VWater linkedWater;

        [Header("Options")]

        [ColorUsage(false, true)]
        [Tooltip("Extra ambient lighting color")]
        public Color lightingColor;
        [Tooltip("Sun light to use")]
        public Light sunLight;

        [Tooltip("Draw underwater fx when your view goes underwater")]
        public bool drawWaterOverlay = true;

        [Tooltip("Enable wave simulation")]
        public bool enableDynamicWaves = true;

        [Tooltip("Render underwater fx in scene view. This sometimes doesn't work properly")]
        public bool testInEditor;

        private new Renderer renderer;
        private Material mat;

        private int _WaveBufferWorldSize = Shader.PropertyToID("_WaveBufferWorldSize");
        private int _WaveBufferWorldPos = Shader.PropertyToID("_WaveBufferWorldPos");
        private bool dynamicsKeywordEnabled;

        MaterialPropertyBlock props;

        private Bounds waterCamVolume;
        private Vector3 lastUpdatePos;
        private Vector3 lastUpdateScl;

        private Color lightColor0;

        // constructs a quad
        private static Mesh overlayMesh
        {
            get
            {
                if (m_overlayMesh == null)
                {
                    m_overlayMesh = new Mesh();

                    var vertices = new Vector3[4]
                    {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0)
                    };
                    m_overlayMesh.vertices = vertices;

                    var tris = new int[6]
                    {
                    // lower left triangle
                    0, 2, 1,
                    // upper right triangle
                    2, 3, 1
                    };
                    m_overlayMesh.triangles = tris;

                    var normals = new Vector3[4]
                    {
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward
                    };
                    m_overlayMesh.normals = normals;

                    var uv = new Vector2[4]
                    {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1)
                    };
                    m_overlayMesh.uv = uv;
                }

                return m_overlayMesh;
            }
        }
        // stores quad
        private static Mesh m_overlayMesh;

        [HideInInspector]
        public bool cullDynamics = false;


        private void Start()
        {
            renderer = GetComponent<Renderer>();

            if (renderer != null)
            {
                if(Application.isPlaying)
                    mat = renderer.material; // instance material at runtime
                else
                    mat = renderer.sharedMaterial;
                mat.SetTexture("_WaveBuffer", VWaterDynamicsManager.waterBuffer);
                dynamicsKeywordEnabled = false;
                mat.DisableKeyword("DYNAMICS_ENABLED");
            }

            if (linkedWater != null) linkedWater.linkedRenderer = this;
        }

        private void OnEnable()
        {
            GenerateWaterBounds();
            RefreshSurfaceRendererBounds();
            StartCoroutine(UpdateBoundsWhenNeeded());
        }

        public void OnDisable()
        {
            foreach (var cam in m_Cameras)
            {
                if (cam.Key)
                {
                    cam.Key.RemoveCommandBuffer(renderTime, cam.Value);
                }
            }
        }



        int _Flow = Shader.PropertyToID("_Flow");
        private void Update()
        {
            if (renderer != null)
            {
                if (props == null)
                    props = new MaterialPropertyBlock();

                props.SetColor(_LightingColor, lightingColor.gamma);
                if(linkedWater != null)
                    props.SetVector(_Flow, new Vector4(linkedWater.globalFlow.x, linkedWater.globalFlow.z, 0, 0));
                renderer.SetPropertyBlock(props);


                if (Application.isPlaying && mat != null)
                {
                    if (!cullDynamics /*&& GameManager.options.graphics_gpuWaterDynamics > 0*/ && enableDynamicWaves && VWaterDynamicsManager.instance != null)
                    {
                        // update dynamic water info for shader
                        mat.SetFloat(_WaveBufferWorldSize, VWaterDynamicsManager.instance.universeSize);
                        mat.SetVector(_WaveBufferWorldPos, VWaterDynamicsManager.instance.universePos);
                        if (!dynamicsKeywordEnabled)
                        {
                            mat.EnableKeyword("DYNAMICS_ENABLED");
                            dynamicsKeywordEnabled = true;
                        }
                    }
                    else
                    {
                        if (dynamicsKeywordEnabled)
                        {
                            mat.DisableKeyword("DYNAMICS_ENABLED");
                            dynamicsKeywordEnabled = false;
                        }
                    }
                }
            }
        }

        IEnumerator UpdateBoundsWhenNeeded()
        {
            yield return null;
            while (true)
            {
                yield return new WaitForFixedUpdate();
                float distFromLastUpdate = (transform.position - lastUpdatePos).sqrMagnitude;
                float sclFromLastUpdate = (transform.lossyScale - lastUpdateScl).sqrMagnitude;
                if (distFromLastUpdate > 0.01f || sclFromLastUpdate > 0.01f)
                {
                    lastUpdatePos = transform.position;
                    lastUpdateScl = transform.lossyScale;
                    GenerateWaterBounds();
                    RefreshSurfaceRendererBounds();
                }
            }
        }

        void GenerateWaterBounds()
        {
            if (linkedWater == null) return;

            Collider[] m_cols = linkedWater.GetComponentsInChildren<Collider>();
            if (m_cols.Length > 0)
            {
                //create bounds combining all collider bounds
                waterCamVolume = m_cols[0].bounds;
                for (int i = 1; i < m_cols.Length; i++)
                {
                    waterCamVolume.Encapsulate(m_cols[i].bounds.max);
                    waterCamVolume.Encapsulate(m_cols[i].bounds.min);
                }
                waterCamVolume.Expand(1f);
            }
        }


        
        void RefreshSurfaceRendererBounds()
        {
            if (linkedWater != null && renderer != null)
            {
                renderer.bounds.Encapsulate(transform.TransformPoint(waterCamVolume.min));
                renderer.bounds.Encapsulate(transform.TransformPoint(waterCamVolume.max));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireCube(waterCamVolume.center, waterCamVolume.size);
        }

        private void OnWillRenderObject()
        {
            if (renderer != null) 
            {
                // cache this for drawing the overlay
                //lightColor0 = Shader.GetGlobalColor(_LightColor0);
                Camera.current.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        const CameraEvent renderTime = CameraEvent.AfterForwardAlpha;
        private Camera[] allCameras;
        private Dictionary<Camera, CommandBuffer> m_Cameras = new Dictionary<Camera, CommandBuffer>();
        private void LateUpdate()
        {
            if (overlayMaterial == null) return;

            allCameras = Camera.allCameras;
            for (int i = 0; i < allCameras.Length; i++)
            {
                UpdateOverlayForCamera(allCameras[i]);
            }
#if UNITY_EDITOR
            // support for scene cameras
            allCameras = UnityEditor.SceneView.GetAllSceneCameras();
            for (int i = 0; i < allCameras.Length; i++)
            {
                UpdateOverlayForCamera(allCameras[i]);
            }
#endif
        }

        void UpdateOverlayForCamera(Camera cam)
        {
            // if the camera is inside our surrounding volume, and we should actually draw this overlay
            if (drawWaterOverlay && (Application.isPlaying || testInEditor) && waterCamVolume.Contains(cam.transform.position))
            {
                if (!m_Cameras.ContainsKey(cam))
                {
                    CommandBuffer buf = new CommandBuffer();
                    buf.name = "Water View Buffer";
                    m_Cameras[cam] = buf;

                    cam.AddCommandBuffer(renderTime, buf);
                    CameraEvents.GetFromCamera(cam).ce_OnPreRender += OnPreRenderForCamera; // register
                }
            }
            else
            {
                if (m_Cameras.ContainsKey(cam))
                { // remove command buffer when camera is not in water
                    cam.RemoveCommandBuffer(renderTime, m_Cameras[cam]);
                    CameraEvents.GetFromCamera(cam).ce_OnPreRender -= OnPreRenderForCamera; // deregister
                    m_Cameras.Remove(cam);
                }
            }
        }


        static int _GlobalWaterHeight = Shader.PropertyToID("_GlobalWaterHeight");
        static int _LightingColor = Shader.PropertyToID("_LightingColor");
        static int _LightColor0 = Shader.PropertyToID("_LightColor0");
        static int _AbsorptionColor = Shader.PropertyToID("_AbsorptionColor");
        static int _ScatteringColor = Shader.PropertyToID("_ScatteringColor");
        static int _Density = Shader.PropertyToID("_Density");

        void OnPreRenderForCamera(Camera cam)
        {
            CommandBuffer buf = m_Cameras[cam];
            buf.Clear();

            if (linkedWater != null)
            { // water parameters
                overlayMaterial.SetFloat(_GlobalWaterHeight, linkedWater.Position);
                overlayMaterial.SetColor(_LightingColor, sunLight != null ? lightingColor + sunLight.color * sunLight.intensity : lightingColor);

                if (renderer != null)
                { // water material parameters
                    overlayMaterial.SetColor(_AbsorptionColor, renderer.sharedMaterial.GetColor(_AbsorptionColor));
                    overlayMaterial.SetColor(_ScatteringColor, renderer.sharedMaterial.GetColor(_ScatteringColor));
                    overlayMaterial.SetFloat(_Density, renderer.sharedMaterial.GetFloat(_Density));
                }
            }

            // scale quad to fill fov of camera
            float scl = Mathf.Tan(cam.fieldOfView / 2 * Mathf.Deg2Rad) * cam.nearClipPlane * 8 * 2;

            // position quad in front of camera
            Matrix4x4 matrix = Matrix4x4.TRS(cam.transform.position + cam.transform.forward * (cam.nearClipPlane * 1.05f),
                cam.transform.rotation, new Vector3(scl, scl, scl));

            buf.DrawMesh(overlayMesh, matrix, overlayMaterial, 0, 0); // grab pass
            buf.DrawMesh(overlayMesh, matrix, overlayMaterial, 0, 1); // overlay pass
        }
    }
}