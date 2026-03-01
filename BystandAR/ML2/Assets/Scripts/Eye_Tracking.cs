using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.XR.MagicLeap;

public class EyeTracking : MonoBehaviour
{
    // public variables //

    // private variables //
    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();

    private MagicLeapInputs mlInputs;
    private MagicLeapInputs.EyesActions eyesActions;

    // for visulization purposes
    private Camera MainCamera;
    [SerializeField]
    private bool VisualizationToggle = false;
    [SerializeField]
    private GameObject EyeGazeVisualizer;
    private Renderer EyeGazeVisualizerRenderer;

    [SerializeField]
    private GameObject PositioningCube;
    private Renderer PositioningCubeRenderer;
    private Collider PositioningCubeCollider;

    private Vector3 lastGazeLocation;
    private Vector3 lastGazeOrigin;
    private Vector3 lastGazeDir;

    [SerializeField]
    private GameObject Logger;
    private CSVLogger CSVLoggerScript;

    [SerializeField]
    private string ReadGazeDataFromCSVName = "GazePos2024-18-6--19-45-33.csv";
    private string GazeDataFile;
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

    private Queue<Vector3> HardcodedGazePosPath = new Queue<Vector3>();
    /*
    private Queue<Vector3> HardcodedGazeOriginPath = new Queue<Vector3>();
    private Queue<Vector3> HardcodedGazeDirPath = new Queue<Vector3>();
    */
    private List<Tuple<int, Vector3>> HardcodedGazeOriginPath = new List<Tuple<int, Vector3>>();
    private List<Tuple<int, Vector3>> HardcodedGazeDirPath = new List<Tuple<int, Vector3>>();

    private Queue<Vector3> HardcodedQRDirPath = new Queue<Vector3>();
    private Queue<float> HardcodedQRDistPath = new Queue<float>();

    private bool toggleButtonState = false;
    private int togglePressCount = 0;

    /*
    private float gazeOriginMinX;
    private float gazeOriginMaxX;

    private float gazeOriginMinY;
    private float gazeOriginMaxY;

    private float gazeOriginMinZ;
    private float gazeOriginMaxZ;
    */

    [SerializeField]
    private GameObject ArucoDetectionObject;
    private ArUcoDetector ArucoDetctionScript;

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

    private Vector3 MainCameraPosition;

    private bool initiallyPositioned = false;

    private int gazeIndex = 1;
    private Stopwatch timer = new Stopwatch();
    private int elapsedMilliseconds = 0;

    [SerializeField]
    private float distScalingFactor = 1.0f;

    /*
    public float boundsBuffer = 0.01f;
    public float DistBoundsBuffer = 0.1f;
    */

    // functions //

    void Awake()
    { 
        // seek eye tracking permissions
        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;

        MLPermissions.RequestPermission(MLPermission.EyeTracking, permissionCallbacks);

        MainCamera = Camera.main;

        // CSV Logger Script
        CSVLoggerScript = Logger.GetComponent<CSVLogger>();

        // if reading data from CSV, ensure it is read fully to the queue
        if (CSVLoggerScript.getReadGazeFromCSV())
        {
            GazeDataFile = Application.dataPath + "/../Logs/" + ReadGazeDataFromCSVName;
            HardcodedGazePosPath = CSVLoggerScript.loadGazeDataCSV(GazeDataFile);

            GazeOriginFile = Application.dataPath + "/../Logs/" + ReadGazeOriginFromCSVName;
            GazeDirFile = Application.dataPath + "/../Logs/" + ReadGazeDirFromCSVName;

            /*
                HardcodedGazeOriginPath = CSVLoggerScript.loadGazeOriginDataCSV(GazeOriginFile);
                HardcodedGazeDirPath = CSVLoggerScript.loadGazeDirDataCSV(GazeDirFile);
            */

            HardcodedGazeOriginPath = CSVLoggerScript.loadListFromGazeOriginDataCSV(GazeOriginFile);
            HardcodedGazeDirPath = CSVLoggerScript.loadListFromGazeDirDataCSV(GazeDirFile);

            QRDirFile = Application.dataPath + "/../Logs/" + ReadQRDirFromCSVName;
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
            Debug.Log("QRDirMinX: " + QRDirMinX);
            Debug.Log("QRDirMaxX: " + QRDirMaxX);
            Debug.Log("QRDirMidX: " + QRDirMidX);
            Debug.Log("QRDirMinY: " + QRDirMinY);
            Debug.Log("QRDirMaxY: " + QRDirMaxY);
            Debug.Log("QRDirMidY: " + QRDirMidY);
            Debug.Log("QRDirMinZ: " + QRDirMinZ);
            Debug.Log("QRDirMaxZ: " + QRDirMaxZ);
            Debug.Log("QRDirMidZ: " + QRDirMidZ);
            */

            QRDistFile = Application.dataPath + "/../Logs/" + ReadQRDistFromCSVName;
            HardcodedQRDistPath = CSVLoggerScript.loadQRDistDataCSV(QRDistFile);

            QRDistMin = CSVLoggerScript.getQRDistMin();
            QRDistMax = CSVLoggerScript.getQRDistMax();
            QRDistMid = (QRDistMax + QRDistMin) / 2;

            /*
            Debug.Log("QRDistMid: " + QRDistMid);
            Debug.Log("QRDistMin: " + QRDistMin);
            Debug.Log("QRDistMax: " + QRDistMax);
            */
        }
    }

    void Start()
    {
        // Debug.Log("EyeTracking // In Start");

        // Initialize Magic Leap Eye Tracking
        InputSubsystem.Extensions.MLEyes.StartTracking();

        // Initialize Magic Leap inputs to capture input data
        mlInputs = new MagicLeapInputs();
        mlInputs.Enable();

        // Initialize Eyes Actions using mlInputs
        eyesActions = new MagicLeapInputs.EyesActions(mlInputs);

        // assign renderer for eye tracking visualizaiton
        EyeGazeVisualizerRenderer = EyeGazeVisualizer.GetComponent<Renderer>();

        PositioningCubeRenderer = PositioningCube.GetComponent<Renderer>();
        PositioningCubeCollider = PositioningCube.GetComponent<Collider>();

        ArucoDetctionScript = ArucoDetectionObject.GetComponent<ArUcoDetector>();
    }

    void Update()
    {
        if (togglePressCount == 1)
        {
            Debug.Log("Starting stopwatch");

            // start timer
            timer.Start();

            togglePressCount++;
        }

        if (togglePressCount > 0)
        {
            elapsedMilliseconds = (int)timer.ElapsedMilliseconds;
        }

        MainCameraPosition = MainCamera.transform.position;

        PositioningCubePosUpdate();

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
            // Debug.Log("EyeTracking // HardcodedGazePosPath.Count: " + HardcodedGazePosPath.Count);
            // Debug.Log("EyeTracking // HardcodedGazeOriginPath.Count " + HardcodedGazeOriginPath.Count);
            // Debug.Log("EyeTracking // HardcodedGazeDirPath.Count: " + HardcodedGazeDirPath.Count);

            if (HardcodedGazePosPath.Count > 0 && HardcodedGazeOriginPath.Count > 0 && HardcodedGazeDirPath.Count > 0)
            {
                lastGazeLocation = HardcodedGazePosPath.Dequeue();
                // Debug.Log("EyeTracking // lastGazeLocation (from file): " + lastGazeLocation);
                
                /*
                lastGazeOrigin = HardcodedGazeOriginPath.Dequeue();
                lastGazeDir = HardcodedGazeDirPath.Dequeue();
                */

                for (int i = gazeIndex; i < HardcodedGazeOriginPath.Count; i++)
                {
                    int currentElapsedTime = elapsedMilliseconds;
                    int storedElapsedTime = HardcodedGazeOriginPath[gazeIndex].Item1; // could have used any of the eye gaze data files to get this

                    Debug.Log("current time: " + currentElapsedTime);
                    Debug.Log("time at index i: " + i + " is: " + storedElapsedTime);

                    // find the exact match
                    if (currentElapsedTime == storedElapsedTime)
                    {
                        Debug.Log("Found exact time match");

                        lastGazeOrigin = HardcodedGazeOriginPath[gazeIndex].Item2;
                        lastGazeDir = HardcodedGazeDirPath[gazeIndex].Item2;

                        gazeIndex = i;

                        break;
                    }

                    // if not exact match then compare the next value. If greater then use the current one
                    // otherwise iterate
                    if (currentElapsedTime < storedElapsedTime)
                    {
                        Debug.Log("Current time is < stored time at current index. Picking the last timestamp's eye gaze");

                        lastGazeOrigin = HardcodedGazeOriginPath[gazeIndex - 1].Item2;
                        lastGazeDir = HardcodedGazeDirPath[gazeIndex - 1].Item2;

                        gazeIndex = i;

                        break;
                    }

                    if (currentElapsedTime > storedElapsedTime)
                    {
                        Debug.Log("Current time is > stored time at current index. Moving to next index");

                        gazeIndex = i; 

                        continue;
                    }
                }

                // lastGazeDir is a Vector3
                lastGazeDir = (lastGazeDir).normalized;
            }
            else
            {
                Debug.Log("Gaze Points ended!!");
                lastGazeLocation = new Vector3(0, 0, 0);
                lastGazeOrigin = new Vector3(0, 0, 0);
                lastGazeDir = new Vector3(0, 0, 0);
            }
        }
        else
        {
            // Read the current value of the eye action.
            var eyes = eyesActions.Data.ReadValue<UnityEngine.InputSystem.XR.Eyes>();

            Vector3 currentFixationPoint = eyes.fixationPoint;
            // Debug.Log("EyeTracking // In Update // currentFixationPoint: " + currentFixationPoint);
            lastGazeLocation = currentFixationPoint;
            lastGazeOrigin = MainCameraPosition;
            lastGazeDir = lastGazeLocation - lastGazeOrigin;
            lastGazeDir = (lastGazeDir).normalized;
        }

        // for visulization purposes
        eyeGazeFixationVisualizer();
    }

    private void OnDestroy()
    {
        // remove eye tracking permissions
        permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied -= OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain -= OnPermissionDenied;

        mlInputs.Dispose();

        InputSubsystem.Extensions.MLEyes.StopTracking();
    }

    private void OnPermissionDenied(string permission)
    {
        // Debug.Log("EyeTracking // In OnPermissionDenied");
    }

    private void OnPermissionGranted(string permission)
    {
        // Debug.Log("EyeTracking // In OnPermissionGranted");

        InputSubsystem.Extensions.MLEyes.StartTracking();
        eyesActions = new MagicLeapInputs.EyesActions(mlInputs);
    }

    public void toggleButtonStateController()
    {
        togglePressCount++;

        toggleButtonState = !toggleButtonState;
    }

    public bool gettoggleButtonState()
    {
        return toggleButtonState;
    }

    public void eyeGazeFixationVisualizer()
    {
        if (VisualizationToggle == true)
        {

            EyeGazeVisualizerRenderer.transform.position = lastGazeOrigin + lastGazeDir * 2;

        }
        else 
        {
            EyeGazeVisualizerRenderer.enabled = false;
        }
    }

    public Vector3 getCurrentFixationPoint()
    {
        // Debug.Log("EyeTracking // In getCurrentFixationPoint");

        return lastGazeLocation;
    }

    public Vector3 getCurrentGazeOrigin()
    {
        return lastGazeOrigin;
    }

    public Vector3 getCurrentGazeDir()
    {
        return lastGazeDir;
    }

    public bool BoundsCheck()
    {
        if (PositioningCubeCollider.bounds.Contains(MainCameraPosition))
        {
            if (!initiallyPositioned)
            {
                initiallyPositioned = true;
                ArucoDetctionScript.OnDisable();
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    public bool getInitiallyPositionedStatus()
    {
        return initiallyPositioned;
    }

    public void PositioningCubePosUpdate()
    {
        // get latest position and rotation of detected QR code 
        Vector3 currQRPosition = ArucoDetctionScript.getLatestPosition();
        Quaternion currQRRotation = ArucoDetctionScript.getLatestRotation();

        // place PositioningCube at certain distance and direction w.r.t. the detected QR code
        /*
        // this works but depth seems a bit off an y axis is totally in negative
        Vector3 angle = (currQRPosition - PositioningCubeRenderer.transform.position).normalized;
        PositioningCubeRenderer.transform.position = currQRPosition + (angle * QRDistMid);
        */

        Vector3 midDir = new Vector3(QRDirMidX, QRDirMidY, QRDirMidZ);
        PositioningCubeRenderer.transform.position = currQRPosition - (midDir.normalized * (QRDistMid * distScalingFactor)); // (QRDistMid/2));

        // Debug.Log("PositioningCubeRenderer.transform.position: " + PositioningCubeRenderer.transform.position);
    }
    
    public int getTogglePressCount()
    {
        return togglePressCount; 
    }

    public void setTogglePressCount()
    {
        togglePressCount++;
    }

    public int getElapsedTimeInMilliseconds()
    {
        return elapsedMilliseconds;
    }
}
