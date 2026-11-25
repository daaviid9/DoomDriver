using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Drives synchronized dual windshield wipers directly on the CarConstruction Element 5 glass shader.
    /// Runs automatically whenever it is raining (scaling sweep speed with rain intensity) and smoothly parks when dry.
    /// </summary>
    public class WindshieldWiperController : MonoBehaviour
    {
        [Header("Sweep Speed & Angles")]
        [Tooltip("Wiper cycles per second in light rain.")]
        [SerializeField] private float lightRainSweepSpeed = 0.75f;
        [Tooltip("Wiper cycles per second in heavy rain / storm.")]
        [SerializeField] private float heavyRainSweepSpeed = 1.25f;
        [Tooltip("Parked resting angle in degrees (measured from straight up, negative = right).")]
        [SerializeField] private float parkAngleDeg = -78f;
        [Tooltip("Maximum upright-left sweep angle in degrees (measured from straight up, positive = left).")]
        [SerializeField] private float maxSweepAngleDeg = 58f;

        [Header("Windshield Geometry Calibration (Auto-Calibrated on Start)")]
        [Tooltip("Auto-computes pivot positions from CarConstruction bounds and Main Camera height.")]
        [SerializeField] private bool autoCalibratePivots = true;
        [Tooltip("Fine-tuning offset (X, Y) applied to both wiper pivots in CarConstruction object space.")]
        [SerializeField] private Vector2 pivotOffsetTuning = new Vector2(0f, -0.34f);
        [Tooltip("Horizontal distance between the left and right wiper pivots.")]
        [SerializeField] private float wiperSpacing = 0.62f;
        [Tooltip("Length of each wiper blade in meters.")]
        [SerializeField] private float wiperLength = 0.70f;

        public bool IsWiping => isWiping;

        private Material targetGlassMaterial;
        private Transform carConstructionTransform;

        private bool isWiping;
        private float currentRainIntensity;

        private float sweepPhase; // 0..2*PI
        private float currentAngleRad;
        private float currentDir = 1f;
        private float activeBlend;

        private Vector2 pivot1;
        private Vector2 pivot2;
        private bool calibrated;

        // Cached shader property IDs
        private static readonly int WiperActivePropId = Shader.PropertyToID("_WiperActive");
        private static readonly int WiperAnglePropId = Shader.PropertyToID("_WiperAngle");
        private static readonly int WiperDirPropId = Shader.PropertyToID("_WiperDir");
        private static readonly int WiperMinAnglePropId = Shader.PropertyToID("_WiperMinAngle");
        private static readonly int WiperMaxAnglePropId = Shader.PropertyToID("_WiperMaxAngle");
        private static readonly int WiperPivot1PropId = Shader.PropertyToID("_WiperPivot1");
        private static readonly int WiperPivot2PropId = Shader.PropertyToID("_WiperPivot2");
        private static readonly int WiperLengthPropId = Shader.PropertyToID("_WiperLength");

        /// <summary>
        /// Binds the runtime glass material and CarConstruction transform from WeatherManager.
        /// </summary>
        public void BindGlassMaterial(Material glassMat, Transform carConstruction)
        {
            targetGlassMaterial = glassMat;
            carConstructionTransform = carConstruction;
            CalibratePivots();
            PushStaticGeometryToShader();
        }

        /// <summary>
        /// Updates current rain intensity (0..1) from WeatherManager so wipers activate automatically in rain.
        /// </summary>
        public void SetRainIntensity(float normalizedRain)
        {
            currentRainIntensity = Mathf.Clamp01(normalizedRain);
        }

        private void Awake()
        {
            currentAngleRad = parkAngleDeg * Mathf.Deg2Rad;
        }

        private void Update()
        {
            if (targetGlassMaterial == null) return;

            bool shouldRun = currentRainIntensity > 0.05f;

            float minRad = parkAngleDeg * Mathf.Deg2Rad;
            float maxRad = maxSweepAngleDeg * Mathf.Deg2Rad;
            float cyclesPerSec = Mathf.Lerp(lightRainSweepSpeed, heavyRainSweepSpeed, currentRainIntensity);

            if (shouldRun)
            {
                isWiping = true;
                activeBlend = Mathf.MoveTowards(activeBlend, 1f, Time.deltaTime * 3.5f);

                float prevSin = Mathf.Sin(sweepPhase - Mathf.PI * 0.5f);
                sweepPhase += Time.deltaTime * cyclesPerSec * Mathf.PI * 2f;
                if (sweepPhase >= Mathf.PI * 2f) sweepPhase -= Mathf.PI * 2f;

                // Smooth 0..1..0 sinusoidal sweep
                float curSin = Mathf.Sin(sweepPhase - Mathf.PI * 0.5f);
                float t = (curSin + 1f) * 0.5f;

                currentAngleRad = Mathf.Lerp(minRad, maxRad, t);
                currentDir = (curSin >= prevSin) ? 1f : -1f;
            }
            else if (isWiping)
            {
                // Finish returning smoothly to parked position before turning off
                float parkSpeed = cyclesPerSec * (maxRad - minRad) * 2.2f;
                currentAngleRad = Mathf.MoveTowards(currentAngleRad, minRad, Time.deltaTime * parkSpeed);
                currentDir = -1f;

                if (Mathf.Approximately(currentAngleRad, minRad))
                {
                    isWiping = false;
                    sweepPhase = 0f;
                }
            }
            else
            {
                activeBlend = Mathf.MoveTowards(activeBlend, 0f, Time.deltaTime * 2.5f);
            }

            targetGlassMaterial.SetFloat(WiperActivePropId, activeBlend);
            targetGlassMaterial.SetFloat(WiperAnglePropId, currentAngleRad);
            targetGlassMaterial.SetFloat(WiperDirPropId, currentDir);

#if UNITY_EDITOR
            // Allow live Inspector tweaking during Play mode
            CalibratePivots();
            PushStaticGeometryToShader();
#endif
        }

        private void CalibratePivots()
        {
            if (carConstructionTransform == null) return;

            float centerX = 0f;
            float baseY = 0.85f;

            if (autoCalibratePivots)
            {
                MeshFilter mf = carConstructionTransform.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    centerX = b.center.x;
                    baseY = b.center.y;
                }

                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camLocal = carConstructionTransform.InverseTransformPoint(cam.transform.position);
                    baseY = camLocal.y;
                }
            }

            Vector2 center = new Vector2(centerX, baseY) + pivotOffsetTuning;
            pivot1 = new Vector2(center.x + wiperSpacing * 0.5f, center.y);
            pivot2 = new Vector2(center.x - wiperSpacing * 0.5f, center.y);
            calibrated = true;
        }

        private void PushStaticGeometryToShader()
        {
            if (targetGlassMaterial == null || !calibrated) return;

            targetGlassMaterial.SetFloat(WiperMinAnglePropId, parkAngleDeg * Mathf.Deg2Rad);
            targetGlassMaterial.SetFloat(WiperMaxAnglePropId, maxSweepAngleDeg * Mathf.Deg2Rad);
            targetGlassMaterial.SetVector(WiperPivot1PropId, new Vector4(pivot1.x, pivot1.y, 0f, 0f));
            targetGlassMaterial.SetVector(WiperPivot2PropId, new Vector4(pivot2.x, pivot2.y, 0f, 0f));
            targetGlassMaterial.SetFloat(WiperLengthPropId, wiperLength);
        }
    }
}
