using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.MagicLeap;

public class EyeTracking : MonoBehaviour
{
    // --- Permissions / ML input ---
    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();
    private MagicLeapInputs mlInputs;
    private MagicLeapInputs.EyesActions eyesActions;

    // --- Scene refs ---
    private Camera mainCamera;

    [Header("EvaluatAR Integration (preferred source of toggle/timer/alignment)")]
    [SerializeField] private EvaluatAR evaluatAR;

    [Header("Visualization (optional)")]
    [SerializeField] private bool visualizationToggle = false;
    [SerializeField] private GameObject eyeGazeVisualizer;
    private Renderer eyeGazeVisualizerRenderer;

    [Header("CSV Replay (legacy - only used if CSVLogger.getReadGazeFromCSV() is true)")]
    [SerializeField] private GameObject logger;
    private CSVLogger csvLogger;

    [SerializeField] private string readGazePosCsvName = "GazePos2024-18-6--19-45-33.csv";
    [SerializeField] private string readGazeOriginCsvName = "GazeOrigin2024-18-6--19-45-33.csv";
    [SerializeField] private string readGazeDirCsvName = "GazeDir2024-18-6--19-45-33.csv";

    private Queue<Vector3> hardcodedGazePosPath = new Queue<Vector3>();
    private List<Tuple<int, Vector3>> hardcodedGazeOriginPath = new List<Tuple<int, Vector3>>();
    private List<Tuple<int, Vector3>> hardcodedGazeDirPath = new List<Tuple<int, Vector3>>();
    private int gazeIndex = 1;

    private Vector3 lastGazeLocation;
    private Vector3 lastGazeOrigin;
    private Vector3 lastGazeDir;

    void Awake()
    {
        // permissions
        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;

        MLPermissions.RequestPermission(MLPermission.EyeTracking, permissionCallbacks);

        mainCamera = Camera.main;

        // CSV logger (legacy replay path)
        if (logger != null)
        {
            csvLogger = logger.GetComponent<CSVLogger>();
        }

        if (csvLogger != null && csvLogger.getReadGazeFromCSV())
        {
            string logsDir = Application.dataPath + "/../Logs/";
            string gazePosFile = Path.Combine(logsDir, readGazePosCsvName);
            string gazeOriginFile = Path.Combine(logsDir, readGazeOriginCsvName);
            string gazeDirFile = Path.Combine(logsDir, readGazeDirCsvName);

            hardcodedGazePosPath = csvLogger.loadGazeDataCSV(gazePosFile);
            hardcodedGazeOriginPath = csvLogger.loadListFromGazeOriginDataCSV(gazeOriginFile);
            hardcodedGazeDirPath = csvLogger.loadListFromGazeDirDataCSV(gazeDirFile);
        }
    }

    void Start()
    {
        // Initialize Magic Leap Eye Tracking
        InputSubsystem.Extensions.MLEyes.StartTracking();

        // Initialize Magic Leap inputs to capture input data
        mlInputs = new MagicLeapInputs();
        mlInputs.Enable();

        // Initialize Eyes Actions using mlInputs
        eyesActions = new MagicLeapInputs.EyesActions(mlInputs);

        if (eyeGazeVisualizer != null)
        {
            eyeGazeVisualizerRenderer = eyeGazeVisualizer.GetComponent<Renderer>();
        }
    }

    void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 camPos = (mainCamera != null) ? mainCamera.transform.position : Vector3.zero;

        bool replayToggleOn = (evaluatAR != null) ? evaluatAR.getToggleButtonState() : false;
        int elapsedMs = (evaluatAR != null) ? evaluatAR.getElapsedTimeInMilliseconds() : 0;

        if (replayToggleOn && csvLogger != null && csvLogger.getReadGazeFromCSV())
        {
            if (hardcodedGazePosPath.Count > 0 && hardcodedGazeOriginPath.Count > 0 && hardcodedGazeDirPath.Count > 0)
            {
                lastGazeLocation = hardcodedGazePosPath.Dequeue();

                gazeIndex = Mathf.Clamp(gazeIndex, 1, hardcodedGazeOriginPath.Count - 1);

                for (int i = gazeIndex; i < hardcodedGazeOriginPath.Count; i++)
                {
                    int storedElapsedTime = hardcodedGazeOriginPath[gazeIndex].Item1;

                    if (elapsedMs == storedElapsedTime)
                    {
                        lastGazeOrigin = hardcodedGazeOriginPath[gazeIndex].Item2;
                        lastGazeDir = hardcodedGazeDirPath[gazeIndex].Item2;
                        gazeIndex = i;
                        break;
                    }

                    if (elapsedMs < storedElapsedTime)
                    {
                        lastGazeOrigin = hardcodedGazeOriginPath[gazeIndex - 1].Item2;
                        lastGazeDir = hardcodedGazeDirPath[gazeIndex - 1].Item2;
                        gazeIndex = i;
                        break;
                    }

                    if (elapsedMs > storedElapsedTime)
                    {
                        gazeIndex = i;
                        continue;
                    }
                }

                lastGazeDir = lastGazeDir.normalized;
            }
            else
            {
                lastGazeLocation = Vector3.zero;
                lastGazeOrigin = Vector3.zero;
                lastGazeDir = Vector3.zero;
            }
        }
        else
        {
            var eyes = eyesActions.Data.ReadValue<UnityEngine.InputSystem.XR.Eyes>();

            lastGazeLocation = eyes.fixationPoint;
            lastGazeOrigin = camPos;

            lastGazeDir = (lastGazeLocation - lastGazeOrigin).normalized;
        }

        // visualization (optional)
        UpdateVisualizer();
    }

    private void UpdateVisualizer()
    {
        if (eyeGazeVisualizerRenderer == null) return;

        if (!visualizationToggle)
        {
            eyeGazeVisualizerRenderer.enabled = false;
            return;
        }

        eyeGazeVisualizerRenderer.enabled = true;

        // Position a marker along the gaze ray.
        eyeGazeVisualizerRenderer.transform.position = lastGazeOrigin + lastGazeDir * 2f;
    }

    private void OnDestroy()
    {
        permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied -= OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain -= OnPermissionDenied;

        if (mlInputs != null)
        {
            mlInputs.Dispose();
        }

        InputSubsystem.Extensions.MLEyes.StopTracking();
    }

    private void OnPermissionDenied(string permission)
    {
    }

    private void OnPermissionGranted(string permission)
    {
        InputSubsystem.Extensions.MLEyes.StartTracking();
        if (mlInputs != null)
        {
            eyesActions = new MagicLeapInputs.EyesActions(mlInputs);
        }
    }

    public Vector3 getCurrentFixationPoint() => lastGazeLocation;
    public Vector3 getCurrentGazeOrigin() => lastGazeOrigin;
    public Vector3 getCurrentGazeDir() => lastGazeDir;


    public bool getReadGazeFromCSVStatus()
    {
        return (csvLogger != null) && csvLogger.getReadGazeFromCSV();
    }

    // --- Legacy API kept for compatibility (forwarded to EvaluatAR when available) ---
    public void toggleButtonStateController()
    {
        if (evaluatAR != null) evaluatAR.toggleButtonStateController();
    }

    public bool gettoggleButtonState()
    {
        return (evaluatAR != null) && evaluatAR.getToggleButtonState();
    }

    public bool getInitiallyPositionedStatus()
    {
        return (evaluatAR != null) && evaluatAR.getIsPositioned();
    }

    public int getTogglePressCount()
    {
        return (evaluatAR != null) ? evaluatAR.getToggleButtonPressCount() : 0;
    }

    public void setTogglePressCount()
    {
        if (evaluatAR != null) evaluatAR.toggleButtonStateController();
    }

    public int getElapsedTimeInMilliseconds()
    {
        return (evaluatAR != null) ? evaluatAR.getElapsedTimeInMilliseconds() : 0;
    }
}
