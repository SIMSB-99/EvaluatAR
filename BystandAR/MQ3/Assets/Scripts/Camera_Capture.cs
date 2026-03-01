using UnityEngine;

using Meta.XR;

using Meta.XR.Samples;
using PassthroughCameraSamples;

using ZXing;

using System;
using System.IO;
// using System.Linq;
using System.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;

public class CameraCapture : MonoBehaviour
{
    [SerializeField]
    private WebCamTextureManager webCamTextureManager;
    [HideInInspector]
    public WebCamTexture cameraFrame;

    private PassthroughCameraEye CameraEye => webCamTextureManager.Eye;

    // [SerializeField]
    // private GameObject environmentRaycastManagerGameObject;
    private EnvironmentRaycastManager environmentRaycastManager;

    private Vector3 currQRPosition = new Vector3(0, 0, 0);
    private Quaternion currQRRotation;

    [SerializeField]
    private GameObject Logger;
    private CSVLogger CSVLoggerScript;

    [HideInInspector]
    public bool toggleButtonState = false;
    private int togglePressCount = 0;
    private int gazeIndex = 1;
    private Stopwatch timer = new Stopwatch();
    private int elapsedMilliseconds = 0;

    [SerializeField]
    private string ReadGazeOriginFromCSVName = "GazeOrigin2024-18-6--19-45-33.csv";
    private string GazeOriginFile;
    [SerializeField]
    private string ReadGazeDirFromCSVName = "GazeDir2024-18-6--19-45-33.csv";
    private string GazeDirFile;
    [SerializeField]
    private string ReadQRDirFromCSVName = "QRDir2024-18-6--19-45-33.csv";
    private string QRDirFile;
    [SerializeField]
    private string ReadQRDistFromCSVName = "QRDist2024-18-6--19-45-33.csv";
    private string QRDistFile;

    private List<Tuple<int, Vector3>> HardcodedGazeOriginPath = new List<Tuple<int, Vector3>>();
    private List<Tuple<int, Vector3>> HardcodedGazeDirPath = new List<Tuple<int, Vector3>>();

    private Queue<Vector3> HardcodedQRDirPath = new Queue<Vector3>();
    private Queue<float> HardcodedQRDistPath = new Queue<float>();

    [HideInInspector]
    public Vector3 direction = new Vector3(0, 0, 0);
    [HideInInspector]
    public Vector3 origin = new Vector3(0, 0, 0);

    private Camera MainCamera;
    private Vector3 MainCameraPosition;

    [SerializeField]
    private GameObject EyeGazeVisualizer;
    private Renderer EyeGazeVisualizerRenderer;
    [SerializeField]
    private bool VisualizationToggle = false;

    [SerializeField]
    private GameObject PositioningCube;
    private Renderer PositioningCubeRenderer;
    private Collider PositioningCubeCollider;

    [HideInInspector]
    public bool initiallyPositioned = false;

    private float QRDistMin;
    private float QRDistMax;
    private float QRDistMid;

    private float QRDirMinX;
    private float QRDirMaxX;
    private float QRDirMidX;

    private float QRDirMinY;
    private float QRDirMaxY;
    private float QRDirMidY;

    private float QRDirMinZ;
    private float QRDirMaxZ;
    private float QRDirMidZ;

    [SerializeField]
    private float distScalingFactor = 1.0f;

    [SerializeField]
    private GameObject BumperOverride;
    private BumperOverride BumperOverrideScript;

    /*
    [HideInInspector]
    public LineRenderer lineRenderer;
    // public Transform rayOrigin;
    [HideInInspector]
    public float rayLength = 5.0f;
    [HideInInspector]
    public Color rayColor = Color.red;
    */

    // initialize vairables before application starts
    void Awake()
    {
        MainCamera = Camera.main;
        
        BumperOverrideScript = BumperOverride.GetComponent<BumperOverride>();
        environmentRaycastManager = GetComponent<EnvironmentRaycastManager>();

        //  CSV Logger Script
        CSVLoggerScript = Logger.GetComponent<CSVLogger>();
        if (CSVLoggerScript.getReadGazeFromCSV())
        {
            GazeOriginFile = Application.persistentDataPath + "/Logs/" + ReadGazeOriginFromCSVName;
            HardcodedGazeOriginPath = CSVLoggerScript.loadListFromGazeOriginDataCSV(GazeOriginFile);

            GazeDirFile = Application.persistentDataPath + "/Logs/" + ReadGazeDirFromCSVName;
            HardcodedGazeDirPath = CSVLoggerScript.loadListFromGazeDirDataCSV(GazeDirFile);

            QRDirFile = Application.persistentDataPath + "/Logs/" + ReadQRDirFromCSVName;
            HardcodedQRDirPath = CSVLoggerScript.loadQRDirDataCSV(QRDirFile);

            QRDirMinX = CSVLoggerScript.getQRDirMinX();
            QRDirMaxX = CSVLoggerScript.getQRDirMaxX();
            QRDirMidX = (QRDirMinX + QRDirMaxX) / 2;

            QRDirMinY = CSVLoggerScript.getQRDirMinY();
            QRDirMaxY = CSVLoggerScript.getQRDirMaxY();
            QRDirMidY = (QRDirMinY + QRDirMaxY) / 2;

            QRDirMinZ = CSVLoggerScript.getQRDirMinZ();
            QRDirMaxZ = CSVLoggerScript.getQRDirMaxZ();
            QRDirMidZ = (QRDirMinZ + QRDirMaxZ) / 2;

            /*
            Debug.Log("BystandAR -- QRDirMinX: " + QRDirMinX);
            Debug.Log("BystandAR -- QRDirMaxX: " + QRDirMaxX);
            Debug.Log("BystandAR -- QRDirMinY: " + QRDirMinY);
            Debug.Log("BystandAR -- QRDirMaxY: " + QRDirMaxY);
            Debug.Log("BystandAR -- QRDirMinZ: " + QRDirMinZ);
            Debug.Log("BystandAR -- QRDirMaxZ: " + QRDirMaxZ);
            */

            QRDistFile = Application.persistentDataPath + "/Logs/" + ReadQRDistFromCSVName;
            HardcodedQRDistPath = CSVLoggerScript.loadQRDistDataCSV(QRDistFile);

            QRDistMin = CSVLoggerScript.getQRDistMin();
            QRDistMax = CSVLoggerScript.getQRDistMax();
            QRDistMid = (QRDistMax + QRDistMin) / 2;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        PositioningCubeRenderer = PositioningCube.GetComponent<Renderer>();
        PositioningCubeCollider = PositioningCube.GetComponent<Collider>();

        EyeGazeVisualizerRenderer = EyeGazeVisualizer.GetComponent<Renderer>();

        /*
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.startColor = rayColor;
            lineRenderer.endColor = rayColor;
        }
        */
    }

    // Update is called once per frame
    void Update()
    {
        MainCameraPosition = MainCamera.transform.position;

        if (togglePressCount == 1)
        {
            Debug.Log("BystandAR -- Starting stopwatch");

            // start timer
            timer.Start();

            togglePressCount++;
        }

        if (togglePressCount > 0)
        {
            elapsedMilliseconds = (int)timer.ElapsedMilliseconds;
        }

        if (webCamTextureManager.WebCamTexture != null)
        {
            cameraFrame = webCamTextureManager.WebCamTexture;

            if ((initiallyPositioned == false) && (BumperOverrideScript.FrameCounter % 20 == 0))
            {
                currQRPosition = detectQRCode();
            }
        }
        
        bool isInBounds = BoundsCheck();
        if (isInBounds)
        {
            EyeGazeVisualizerRenderer.material.color = Color.green;
        }
        else
        {
            EyeGazeVisualizerRenderer.material.color = Color.red;
        }

        if (toggleButtonState && CSVLoggerScript.getReadGazeFromCSV())
        {
            if (HardcodedGazeOriginPath.Count > 0 && HardcodedGazeDirPath.Count > 0)
            {
                for (int i = gazeIndex; i < HardcodedGazeOriginPath.Count; i++)
                {
                    int currentElapsedTime = elapsedMilliseconds;
                    int storedElapsedTime = HardcodedGazeOriginPath[gazeIndex].Item1; // could have used any of the eye gaze data files to get this

                    // find the exact match
                    if (currentElapsedTime == storedElapsedTime)
                    {
                        origin = HardcodedGazeOriginPath[gazeIndex].Item2;
                        direction = HardcodedGazeDirPath[gazeIndex].Item2;

                        gazeIndex = i;

                        break;
                    }

                    // if not exact match then compare the next value. If greater then use the current one
                    // otherwise iterate
                    if (currentElapsedTime < storedElapsedTime)
                    {
                        origin = HardcodedGazeOriginPath[gazeIndex - 1].Item2;
                        direction = HardcodedGazeDirPath[gazeIndex - 1].Item2;

                        gazeIndex = i;

                        break;
                    }

                    if (currentElapsedTime > storedElapsedTime)
                    {
                        gazeIndex = i;

                        continue;
                    }
                }

                direction = (direction).normalized;
            }
            else
            {
                origin = new Vector3(0, 0, 0);
                direction = new Vector3(0, 0, 0);
            }
        }

        eyeGazeFixationVisualizer();
    }

    public void toggleButtonStateController()
    {
        togglePressCount++;

        toggleButtonState = !toggleButtonState;
    }

    private void eyeGazeFixationVisualizer()
    {
        if (VisualizationToggle == true)
        {
            // EyeGazeVisualizerRenderer.transform.position = origin + direction * 2;
            EyeGazeVisualizerRenderer.transform.position = MainCameraPosition + direction * 2;
        }
        else
        {
            EyeGazeVisualizerRenderer.enabled = false;
        }
    }

    public Vector3 cameraToWorldConversion(int x, int y)
    {
        // CameraHeadPose = CenterEyeAnchor.transform.ToOVRPose();

        var cameraScreenPoint = new Vector2Int(x, y);
        var ray = PassthroughCameraUtils.ScreenPointToRayInWorld(CameraEye, cameraScreenPoint);

        // By default, set the hit point at a fixed length away.
        Vector3 hitPoint = ray.GetPoint(3);

        if (environmentRaycastManager.Raycast(ray, out var hitInfo))//, 100, mask))
        {
            // Debug.LogError("BystandAR -- cameraToWorldConversion raycast hit something");

            hitPoint = hitInfo.point;
        }

        return hitPoint;
    }

    public Vector2 worldToCameraConversion(Vector3 worldPoint)
    {
        var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);
        var localPoint = Quaternion.Inverse(cameraPose.rotation) * (worldPoint - cameraPose.position);
        var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(CameraEye);

        if (localPoint.z <= 0.0001f)
        {
            // Debug.LogWarning("BystandAR -- point too close");
            return Vector2.zero;
        }

        var u = (intrinsics.FocalLength.x * (localPoint.x / localPoint.z) + intrinsics.PrincipalPoint.x) / cameraFrame.width;
        var vNorm = (intrinsics.FocalLength.y * (localPoint.y / localPoint.z) + intrinsics.PrincipalPoint.y) / cameraFrame.height;
        var v = 1.0f - vNorm;
        Vector2 uv = new Vector2(u, v);

        // return uv;

        var x = Mathf.Clamp(Mathf.RoundToInt(uv.x * cameraFrame.width), 0, cameraFrame.width - 1);
        var y = Mathf.Clamp(Mathf.RoundToInt(uv.y * cameraFrame.height), 0, cameraFrame.height - 1);
        Vector2 pixelPoint = new Vector2(x, y);

        return pixelPoint;
    }

    public Vector3 detectQRCode()
    {
        IBarcodeReader barcodeReader = new BarcodeReader
        {
            AutoRotate = true,
            TryInverted = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true
            }
        };

        Result result = barcodeReader.Decode(cameraFrame.GetPixels32(), cameraFrame.width, cameraFrame.height);

        if (result != null)
        {
            Vector2Int qrCodeCenter = GetQrCodeCenter(result.ResultPoints, cameraFrame.height);
            // flip Y so it matches the Passthrough / screen coordinate system
            qrCodeCenter.y = cameraFrame.height - 1 - qrCodeCenter.y;
            // Debug.LogError("BystandAR -- QRCode detected. It's center is: " + qrCodeCenter);

            Pose pose = QRCodeConvertScreenPointToWorldPoint(qrCodeCenter);

            currQRPosition = pose.position;
            currQRRotation = pose.rotation;

            // Debug.LogError("BystandAR -- World QR code position: " + currQRPosition);

            PositioningCubePosUpdate();

            return currQRPosition;
        }
        else
        {
            return Vector3.zero;
        }
    }

    private Vector2Int GetQrCodeCenter(ResultPoint[] resultPoints, int textureHeight)
    {
        if (resultPoints == null || resultPoints.Length == 0)
        {
            return Vector2Int.zero;
        }

        float sumX = 0;
        float sumY = 0;

        foreach (var point in resultPoints)
        {
            sumX += point.X;
            sumY += point.Y;
        }

        float x = sumX / resultPoints.Length;
        float y = sumY / resultPoints.Length;

        int centerX = Mathf.RoundToInt(x);
        int centerY = Mathf.RoundToInt(textureHeight - y);

        return new Vector2Int(centerX, centerY);
    }

    private Pose QRCodeConvertScreenPointToWorldPoint(Vector2Int screenPoint)
    {
        var ray = PassthroughCameraUtils.ScreenPointToRayInWorld(CameraEye, screenPoint);

        /*
        Vector3 rayEndPoint = ray.origin + ray.direction * rayLength;
        // Update the LineRenderer's position
        lineRenderer.SetPosition(0, ray.origin);
        lineRenderer.SetPosition(1, rayEndPoint);

        Debug.LogError("BystandAR -- headsetPos: " + MainCameraPosition);
        Debug.LogError("BystandAR -- ray.origin: " + ray.origin);
        Debug.LogError("BystandAR -- ray.direction: " + ray.direction);
        */

        // By default, set the hit point at a fixed length away.
        Vector3 hitPoint = ray.GetPoint(3);

        if (environmentRaycastManager.Raycast(ray, out var hitInfo))
        {
            // Debug.LogError("BystandAR -- QRCode screen to world ray hit something");

            Pose pose = new Pose(hitInfo.point, Quaternion.FromToRotation(Vector3.up, hitInfo.normal));
            return pose;
        }
        else
        {
            return Pose.identity;
        }
    }

    public void PositioningCubePosUpdate()
    {
        // place PositioningCube at certain distance and direction w.r.t. the detected QR code
        /*
        // this works but depth seems a bit off an y axis is totally in negative
        Vector3 angle = (currQRPosition - PositioningCubeRenderer.transform.position).normalized;
        PositioningCubeRenderer.transform.position = currQRPosition + (angle * QRDistMid);
        */

        Vector3 midDir = new Vector3(QRDirMidX, QRDirMidY, QRDirMidZ);
        // Debug.LogError("BystandAR -- midDir: " + midDir);

        Vector3 positioningCubePos = currQRPosition - (midDir.normalized * (QRDistMid * distScalingFactor)); // (QRDistMid/2));

        // Debug.LogError("BystandAR -- currQRPosition: " + currQRPosition);
        // Debug.LogError("BystandAR -- positioningCubePos: " + positioningCubePos);

        PositioningCubeRenderer.transform.position = positioningCubePos;
    }

    public bool BoundsCheck()
    {
        if (PositioningCubeCollider.bounds.Contains(MainCameraPosition))
        {
            if (!initiallyPositioned)
            {
                Debug.LogError("BystandAR -- POSITIONED!!");
                initiallyPositioned = true;
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    public void FPSCalculator(GameObject[] faceCubesInScene)
    {
        int FPS = (int)(1.0f / Time.smoothDeltaTime);

        bool qrDetectionStatus = initiallyPositioned;
        qrDetectionStatus = !qrDetectionStatus;

        string finalJSONString = "{";
        //  create JSON like data structure that stores all info regarding all faceCubesInScene
        for (int i = 0; i < faceCubesInScene.Length; i++)
        {
            var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

            string faceCubeName = "\"" + faceCubesInScene[i].name + "\": {";

            Vector3 faceCubeWorldPos = faceCubesInScene[i].transform.position;
            string faceCubeWorldPosString = "\"3D Position\": \"(" + faceCubeWorldPos.x + " | " + faceCubeWorldPos.y + " | " + faceCubeWorldPos.z + ")\" |";

            bool faceCubeIsSubject = faceCubeScript.getIsSubject();
            string faceCubeIsSubjectString = "\"isSubject\": \"" + faceCubeIsSubject + "\" |";

            /*
            Vector3 predictedPosition = faceCubeScript.predictedPosition;
            string faceCubePredictedPosString = "\"Predicted Position\": \"(" + predictedPosition.x + " | " + predictedPosition.y + " | " + predictedPosition.z + ")\" |";

            Vector2 predicted2DPosition = CameraUtilities.ConvertWorldPointToScreen(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, predictedPosition);
            string faceCubePredicted2DPosString = "\"Predicted Position 2D\": \"(" + predicted2DPosition.x + " | " + predicted2DPosition.y + ")\" |";
            */

            double faceCube2DXCenter = faceCubeScript.xCenter;
            double faceCube2DYCenter = faceCubeScript.yCenter;
            string faceCube2DPosString = "\"2D Position\": \"(" + faceCube2DXCenter + " | " + faceCube2DYCenter + ")\" }";

            // string faceCubeJSONString = faceCubeName + faceCubeWorldPosString + faceCubeIsSubjectString + faceCubePredictedPosString + faceCubePredicted2DPosString + faceCube2DPosString + " |";
            string faceCubeJSONString = faceCubeName + faceCubeWorldPosString + faceCubeIsSubjectString + faceCube2DPosString + " |";

            finalJSONString += faceCubeJSONString;
        }
        finalJSONString += '}';

        // log FPS to CSV
        string writeDateTime = DateTime.Now.ToString("HH-mm-ss.ffff");
        string FPSFileLine = writeDateTime + "," + FPS.ToString() + "," + faceCubesInScene.Length.ToString() + "," + BumperOverrideScript.FrameCounter + "," + qrDetectionStatus + "," + toggleButtonState + "," + finalJSONString;

        CSVLoggerScript.addFPStoList(FPSFileLine);
    }

}
