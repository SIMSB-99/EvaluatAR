using UnityEngine;

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using Debug = UnityEngine.Debug;


public class EvaluatAR : MonoBehaviour
{
    // private variables //
    private int frameNumber = 0;
    private string sessionId = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
    // Per-row timestamp (matches old CSVLogger format)
    private static string NowTimestamp() => DateTime.Now.ToString("HH-mm-ss.ffff");
    private string deviceName = "";
    private Renderer toggleButtonRenderer;
    private bool toggleButtonState = false;
    private int toggleButtonPressCount = 0;
    private Renderer positioningCubeRenderer;
    private Collider positioningCubeCollider;
    private bool isPositioned = false;
    private bool isReferenceImageCaptured = false;
    private Vector3 currentQrCodePosition;
    private Quaternion currentQrCodeRotation;
    private float currentQrCodeDistance;
    private Vector3 currentQrCodeDirection;
    private Renderer alignmentVisualizerRenderer;
    private string logsFolderPath = "";
    private string dataCollectionLogsFilePath = "";
    private string dataReplayLogsFilePath = "";
    private string imagesFilePath = "";
    private string currentModeLogsPath = "";
    private string logsToReplayFilePath = "";
    private TextWriter logsDataTw;
    private List<string> logsDataList = new List<string>(); // TODO: update this datatype when the list of data in CSVs is finalized
    private long totalLogsSize = 0;
    private Encoding csvEncoding = Encoding.UTF8;
    private int newlineBytes = Encoding.UTF8.GetByteCount(System.Environment.NewLine);
    private Texture2D currentCameraCapture;
    private Camera MainCamera;
    private Vector3 MainCameraTransformPosition;
    private Quaternion MainCameraTransformRotation;
    private List<int> storedElapsedTimeArray = new List<int>();
    private Queue<float> QRDistanceArray = new Queue<float>();
    private List<float> QRDirectionXArray = new List<float>();
    private List<float> QRDirectionYArray = new List<float>();
    private List<float> QRDirectionZArray = new List<float>();
    private List<Vector3> gazeOriginArray = new List<Vector3>();
    private List<Vector3> gazeDirectionArray = new List<Vector3>();
    private float QRDistanceMin;
    private float QRDistanceMax;
    private float QRDistanceMid;
    private float QRDirectionMinX;
    private float QRDirectionMaxX;
    private float QRDirectionMidX;
    private float QRDirectionMinY;
    private float QRDirectionMaxY;
    private float QRDirectionMidY;
    private float QRDirectionMinZ;
    private float QRDirectionMaxZ;
    private float QRDirectionMidZ;
    private Vector3 midDirection;
    private Stopwatch timer = new Stopwatch();
    private int elapsedMilliseconds = 0;
    private int replayDataItems = 3;    // TODO: set to the number of data items stored for replay
    private List<Vector3> currentFrameReplayData = new List<Vector3>();
    private int replayDataPoint = 1;
    private bool isReplayDataFinished = false;
    private ArUcoDetector qrCodeDetectorScript; // TODO: update the datatype to the class name in script if needed
    private bool qrDetectionStatus = true;

    [SerializeField]
    private GameObject toggleButton;
    [SerializeField]
    private Vector3 toggleButtonOffset = new Vector3(-0.2f, -0.3f, 0.01f);
    [SerializeField]
    private GameObject positioningCube;
    [SerializeField]
    private GameObject alignmentVisualizer;
    [SerializeField]
    private Vector3 alignmentVisualizerOffset = new Vector3(-0.22f, 0.3f, 0.01f);
    public bool dataCollectionMode = true;
    public bool dataReplayMode = false;
    [SerializeField]
    [Tooltip("Name of the file used to replay data from if EvaluatAR is run in Data Replay mode.")]
    private string logsToReplayFileName = "DataCollection-2024-18-6--19-45-33.csv";
    [SerializeField]
    [Tooltip("Multiplier used to scale the position of the Positioning Cube.")]
    private float distanceScalingFactor = 1.0f;
    [SerializeField]
    private GameObject qrCodeDetector;

    void Awake()
    {
        MainCamera = Camera.main;

        // TODO: set path according to device
        //logsFolderPath = Application.persistentDataPath + "/Logs/"; // HL2
        logsFolderPath = Application.dataPath + "/../Logs/"; // ML2
        if (!Directory.Exists(logsFolderPath))
        {
            Directory.CreateDirectory(logsFolderPath);
        }
        Debug.Log("Logs Path: " + logsFolderPath);

        dataCollectionLogsFilePath = logsFolderPath + "DataCollection-" + sessionId + ".csv";
        dataReplayLogsFilePath = logsFolderPath + "DataReplay-" + sessionId + ".csv";
        imagesFilePath = logsFolderPath + "ReferenceImage-DataReplay-" + sessionId + ".png";
        logsToReplayFilePath = logsFolderPath + logsToReplayFileName;

        currentFrameReplayData = new List<Vector3>(replayDataItems); // TODO: update datatype of this structure to match how the replay data is stored

        if (dataReplayMode)
        { // read data from file before application loads
            loadReplayDataFromLogs();
        }
    }

    void Start()
    {
        deviceName = SystemInfo.deviceName;

        toggleButtonRenderer = toggleButton.GetComponent<Renderer>();
        positioningCubeRenderer = positioningCube.GetComponent<Renderer>();
        positioningCubeCollider = positioningCube.GetComponent<Collider>();
        alignmentVisualizerRenderer = alignmentVisualizer.GetComponent<Renderer>();
        qrCodeDetectorScript = qrCodeDetector.GetComponent<ArUcoDetector>();    // TODO: update the datatype to the class name in script if needed

        if (dataCollectionMode)
        {
            positioningCube.SetActive(false);
            alignmentVisualizer.SetActive(false);

            currentModeLogsPath = dataCollectionLogsFilePath;
            logsDataTw = new StreamWriter(dataCollectionLogsFilePath, false);
            logsDataTw.WriteLine("Timestamp, ElapsedTime, frame #, FPS, QRDistance, QRDirection-x, QRDirection-y, QRDirection-z, logSize, logData"); // TODO: double check to confirm if this list has all the required data
            logsDataTw.Close();
        }
        else if (dataReplayMode)
        {
            currentModeLogsPath = dataReplayLogsFilePath;
            logsDataTw = new StreamWriter(dataReplayLogsFilePath, false);
            logsDataTw.WriteLine("TimeStamp, ElapsedTime, frame #, FPS, logSize, QRDetection Status, isReplayed, PETOutputs"); // TODO: revist to confirm if all this data is enough
            logsDataTw.Close();
        }
    }

    void Update()
    {
        MainCameraTransformPosition = MainCamera.transform.position;
        MainCameraTransformRotation = MainCamera.transform.rotation;
        updateToggleButtonPosition();

        if (toggleButtonPressCount == 1)
        { // start stopwatch if toggle button has been pressed
            timer.Start();
            toggleButtonPressCount++;
        }
        if (toggleButtonPressCount > 0)
        { // keep updating elapsed time once toggle has been pressed
            elapsedMilliseconds = (int)timer.ElapsedMilliseconds;
            string sessionId = DateTime.Now.ToString("HH-mm-ss.ffff");
        }

        if (dataCollectionMode)
        {
            currentQrCodeDistance = qrCodeDetectorScript.getCurrQrDistance();
            currentQrCodeDirection = qrCodeDetectorScript.getCurrQrDirection();
        }
        else if (dataReplayMode)
        {
            if (!isPositioned)
            {
                currentQrCodePosition = qrCodeDetectorScript.getCurrQrPosition();
                updatePositioningCubePosition();
            }
            else
            { // aligned, capture reference POV once and disable QR code detection
                if (!isReferenceImageCaptured)
                {
                    capturePovImage();
                    isReferenceImageCaptured = true;
                }
                if (qrDetectionStatus)
                {
                    qrCodeDetectorScript.OnDisable();
                    qrDetectionStatus = false;
                }
            }

            checkHeadAndPositioningCubeAlignment();
            updateAlignmentVisualizerPositionAndColor();

            if (toggleButtonState)
            {
                if (replayDataPoint >= storedElapsedTimeArray.Count)
                { // replay data finished
                    isReplayDataFinished = true;
                }
                else
                {
                    for (int i = replayDataPoint; i < storedElapsedTimeArray.Count; i++)
                    { // find the best time-matched data for replay
                        if (elapsedMilliseconds == storedElapsedTimeArray[replayDataPoint])
                        {
                            //currentFrameReplayData.Add(gazeOriginArray[replayDataPoint]);
                            //currentFrameReplayData.Add(gazeDirectionArray[replayDataPoint].normalized);

                            replayDataPoint = i;
                            break;
                        }
                        else if (elapsedMilliseconds < storedElapsedTimeArray[replayDataPoint])
                        {
                            //currentFrameReplayData.Add(gazeOriginArray[replayDataPoint - 1]);
                            //currentFrameReplayData.Add(gazeDirectionArray[replayDataPoint - 1].normalized);

                            replayDataPoint = i;
                            break;
                        }
                        else if (elapsedMilliseconds > storedElapsedTimeArray[replayDataPoint])
                        {
                            replayDataPoint = i;
                            continue;
                        }
                    }
                }
            }
        }

        frameNumber++;
    }

    void OnApplicationPause()
    {
        batchWriteToLogFile();
        logsDataList = new List<string>(); // TODO: update this datatype when the list of data in CSVs is finalized
    }

    private void batchWriteToLogFile()
    {
        logsDataTw = new StreamWriter(currentModeLogsPath, true); // true to indicate no overwriting, just append
        for (int i = 0; i < logsDataList.Count; i++)
        {
            logsDataTw.WriteLine(logsDataList[i]);
        }
        logsDataTw.Close();
    }

    public void writeToLogsFile(string logData)
    {   // HOOK; TODO: check if one function works for DC and DR both
        int fps = (int)(1.0f / Time.smoothDeltaTime);
        long currentLogsSize = computeLogsSize(logData);

        if (dataCollectionMode)
        {   // EvaluatAR and PET input data are stored
            string timeStampedFrameAndQrData = NowTimestamp() + "," + elapsedMilliseconds.ToString() + "," + frameNumber.ToString() + "," + fps.ToString() + "," + currentQrCodeDistance.ToString() + "," + currentQrCodeDirection.x.ToString() + "," + currentQrCodeDirection.y.ToString() + "," + currentQrCodeDirection.z.ToString() + "," + currentLogsSize.ToString();
            string dataLine = timeStampedFrameAndQrData + "," + logData;
            logsDataList.Add(dataLine);
        }
        else if (dataReplayMode)
        {  // EvaluatAR and PET outputs are stored
            string timeStampedFrameAndReplayStatusData = NowTimestamp() + "," + elapsedMilliseconds.ToString() + "," + frameNumber.ToString() + "," + fps.ToString() + "," + currentLogsSize.ToString() + "," + qrDetectionStatus.ToString() + "," + toggleButtonState.ToString();
            string dataLine = timeStampedFrameAndReplayStatusData + "," + logData;
            logsDataList.Add(dataLine);
        }
    }

    private long computeLogsSize(string logLine)
    {
        totalLogsSize = totalLogsSize + csvEncoding.GetByteCount(logLine) + newlineBytes;
        return totalLogsSize;
    }

    public Tuple<bool, List<Vector3>> getCurrentFrameData()
    { // HOOK
        return new Tuple<bool, List<Vector3>>(isReplayDataFinished, currentFrameReplayData);
    }

    private void loadReplayDataFromLogs()
    { // TODO: match the preprocessing steps to the columns in the data collection log file
        if (File.Exists(logsToReplayFilePath))
        {
            StreamReader strReader = new StreamReader(logsToReplayFilePath);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                // Skip CSV header rows
                if (line.StartsWith("Timestamp") || line.StartsWith("TimeStamp"))
                {
                    continue;
                }

                var data_values = line.Split(',');

                int timestamp = Int32.Parse(data_values[0]);
                if (data_values.Length < 8) { continue; }
                if (!Int32.TryParse(data_values[1], out int storedElapsedTime)) { continue; }
                int frameNumber = Int32.Parse(data_values[2]);
                int fps = Int32.Parse(data_values[3]);
                if (!float.TryParse(data_values[4], out float qrDistance)) { continue; }
                if (!float.TryParse(data_values[5], out float qdx) || !float.TryParse(data_values[6], out float qdy) || !float.TryParse(data_values[7], out float qdz)) { continue; }
                Vector3 qrDirection = new Vector3(qdx, qdy, qdz);
                Vector3 gazePosition = new Vector3(float.Parse(data_values[8]), float.Parse(data_values[9]), float.Parse(data_values[10]));
                Vector3 gazeOrigin = new Vector3(float.Parse(data_values[11]), float.Parse(data_values[12]), float.Parse(data_values[13]));
                Vector3 gazeDirection = new Vector3(float.Parse(data_values[14]), float.Parse(data_values[15]), float.Parse(data_values[16]));

                // format data for ordered playback
                storedElapsedTimeArray.Add(storedElapsedTime);

                QRDistanceArray.Enqueue(qrDistance);

                QRDirectionXArray.Add(qrDirection.x);
                QRDirectionYArray.Add(qrDirection.y);
                QRDirectionZArray.Add(qrDirection.z);

                gazePositionArray.Enqueue(gazeVector);
                gazeOriginArray.Add(gazeOrigin);
                gazeDirectionArray.Add(gazeDirection);
            }
        }
        else
        {
            //Debug.Log("EvaluatAR // loadReplayDataFromLogs // DR mode; log file not found while reading data");
            return;
        }

        // calculate min, max, and mid values for QR code data for repositioning purposes
        var QRDistanceActualArray = QRDistanceArray.ToArray();
        QRDistanceMin = QRDistanceActualArray.Min();
        QRDistanceMax = QRDistanceActualArray.Max();
        QRDistanceMid = (QRDistanceMin + QRDistanceMax) / 2;

        var QRDirectionXActualArray = QRDirectionXArray.ToArray();
        var QRDirectionYActualArray = QRDirectionYArray.ToArray();
        var QRDirectionZActualArray = QRDirectionZArray.ToArray();
        QRDirectionMinX = QRDirectionXActualArray.Min();
        QRDirectionMaxX = QRDirectionXActualArray.Max();
        QRDirectionMidX = (QRDirectionMinX + QRDirectionMaxX) / 2;
        QRDirectionMinY = QRDirectionYActualArray.Min();
        QRDirectionMaxY = QRDirectionYActualArray.Max();
        QRDirectionMidY = (QRDirectionMinY + QRDirectionMaxY) / 2;
        QRDirectionMinZ = QRDirectionZActualArray.Min();
        QRDirectionMaxZ = QRDirectionZActualArray.Max();
        QRDirectionMidZ = (QRDirectionMinZ + QRDirectionMaxZ) / 2;
        midDirection = new Vector3(QRDirectionMidX, QRDirectionMidY, QRDirectionMidZ);
        midDirection = midDirection.normalized;
    }

    public void toggleButtonStateController()
    { // TODO: bind to the toggle button gameobject
        toggleButtonPressCount++;
        toggleButtonState = !toggleButtonState;
    }

    private void updateToggleButtonPosition()
    {
        toggleButtonRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + toggleButtonOffset;
        toggleButtonRenderer.transform.rotation = MainCameraTransformRotation;
    }

    private void updatePositioningCubePosition()
    {
        positioningCubeRenderer.transform.position = currentQrCodePosition - (midDirection * (QRDistanceMid * distanceScalingFactor));
    }

    private void updateAlignmentVisualizerPositionAndColor()
    {
        alignmentVisualizerRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + alignmentVisualizerOffset;
        alignmentVisualizerRenderer.transform.rotation = MainCameraTransformRotation;

        if (isPositioned)
        {
            alignmentVisualizerRenderer.material.color = Color.green;
        }
        else
        {
            alignmentVisualizerRenderer.material.color = Color.red;
        }
    }

    public void setCurrentCameraCapture(Texture2D currentFrame)
    { // HOOK; TODO: call every frame
        currentCameraCapture = currentFrame;
        // currentCameraCapture.Apply();
    }

    private void capturePovImage()
    {
        currentCameraCapture.Apply();
        File.WriteAllBytes(imagesFilePath, currentCameraCapture.EncodeToPNG());
    }

    private void checkHeadAndPositioningCubeAlignment()
    {
        if (positioningCubeCollider.bounds.Contains(MainCameraTransformPosition))
        {
            if (!isPositioned)
            {
                isPositioned = true;
            }
        }
    }
}
