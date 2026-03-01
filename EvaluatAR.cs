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
    private int frameNumber = 0;
    private string currentDateTime = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
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
    private string logsFolderPath = Application.dataPath + "/../Logs/"; // for HL2 and MQ3, replace with Application.persistentDataPath + "/Logs/";
    private string dataCollectionLogsFilePath = "";
    private string dataReplayLogsFilePath = "";
    private string imagesFilePath = "";
    private string currentModeLogsPath = "";
    private string logsToReplayFilePath = "";
    private TextWriter logsDataTw;
    private List<string> logsDataList = new List<string>(); // this datatype gets updated when the list of logged data in CSVs changes
    private long totalLogsSize = 0;
    private Encoding csvEncoding = Encoding.UTF8;
    private int newlineBytes = Encoding.UTF8.GetByteCount(System.Environment.NewLine);
    private Texture2D currentCameraCapture;
    private Camera MainCamera;
    private Vector3 MainCameraTransformPosition;
    private List<int> storedElapsedTimeArray = new List<int>();
    private Queue<float> QRDistanceArray = new Queue<float>();
    private List<float> QRDirectionXArray = new List<float>();
    private List<float> QRDirectionYArray = new List<float>();
    private List<float> QRDirectionZArray = new List<float>();
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
    private int replayDataItems = 3;    // set to the number of data items stored for replay
    private List<Vector3> currentFrameReplayData = new List<Vector3>();
    private int replayDataPoint = 1;
    private bool isReplayDataFinished = false;
    private ArUcoDetector qrCodeDetectorScript; // update the datatype to the class name in script if needed
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
    private Vector3 alignmentVisualizerOffset = new Vector3(-0.2f, -0.3f, 0.01f);
    [SerializeField]
    private bool dataCollectionMode = true;
    [SerializeField]
    private bool dataReplayMode = false;
    [SerializeField]
    [Tooltip("Name of the file used to replay data from if EvaluatAR is run in Data Replay mode.")]
    private string logsToReplayFileName = "DataCollection-2024-18-6--19-45-33.csv";
    [SerializeField]
    [Tooltip("Multiplier used to scale the position of the Positioning Cube.")]
    private float distanceScalingFactor = 1.0f;
    [SerializeField]
    private GameObject qrCodeDetector;

    void Awake () {
        MainCamera = Camera.main;

        if (!Directory.Exists(logsFolderPath)) {
            Directory.CreateDirectory(logsFolderPath);
        }

        dataCollectionLogsFilePath = logsFolderPath + "DataCollection-" + currentDateTime + ".csv";
        dataReplayLogsFilePath = logsFolderPath + "DataReplay-" + currentDateTime + ".csv";
        imagesFilePath = logsFolderPath + "ReferenceImage-DataReplay-" + currentDateTime + ".png";
        logsToReplayFilePath = logsFolderPath + logsToReplayFileName;

        currentFrameReplayData = new List<Vector3>(replayDataItems); // update datatype of this structure to match how the replay data is stored

        if (dataReplayMode) { // read data from file before application loads
            loadReplayDataFromLogs(); 
        }
    }

    void Start() {
        deviceName = SystemInfo.deviceName;

        toggleButtonRenderer = toggleButton.GetComponent<Renderer>();
        positioningCubeRenderer = positioningCube.GetComponent<Renderer>();
        positioningCubeCollider = positioningCube.GetComponent<Collider>();
        alignmentVisualizerRenderer = alignmentVisualizer.GetComponent<Renderer>();
        qrCodeDetectorScript = qrCodeDetector.GetComponent<ArUcoDetector>();    // update the class name if needed

        if (dataCollectionMode) {
            positioningCube.SetActive(false);
            alignmentVisualizer.SetActive(false);

            currentModeLogsPath = dataReplayLogsFilePath;
            logsDataTw = new StreamWriter(dataReplayLogsFilePath, false);
            logsDataTw.WriteLine("Timestamp, ElapsedTime, frame #, FPS, QRDistance, QRDirection-x, QRDirection-y, QRDirection-z, logSize, logData"); //  update this to log additional data in Data Collection mode
            logsDataTw.Close();
        }
        else if (dataReplayMode) {
            currentModeLogsPath = dataCollectionLogsFilePath;
            logsDataTw = new StreamWriter(dataCollectionLogsFilePath, false);
            logsDataTw.WriteLine("TimeStamp, ElapsedTime, frame #, FPS, logSize, QRDetection Status, isReplayed, DetectedFaces Info"); // update this to log additional data in Data Replay mode. PET Outputs is a JSON-like structure that stores per-frame PET state and privacy outputs
            logsDataTw.Close();
        }
    }

    void Update() {
        MainCameraTransformPosition = MainCamera.transform.position;
        MainCameraTransformRotation = MainCamera.transform.rotation;
        updateToggleButtonPosition();

        if (toggleButtonPressCount == 1) { // start stopwatch if toggle button has been pressed
            timer.Start();
            toggleButtonPressCount++;
        }
        if (toggleButtonPressCount > 0) { // keep updating elapsed time once toggle has been pressed
            elapsedMilliseconds = (int)timer.ElapsedMilliseconds;
            string currentDateTime = DateTime.Now.ToString("HH-mm-ss.ffff");
        }

        if (dataCollectionMode) {   
            currentQrCodeDistance = qrCodeDetectorScript.getCurrQrDistance();
            currentQrCodeDirection = qrCodeDetectorScript.getCurrQrDirection();
        }
        else if (dataReplayMode) {
            if (!isPositioned) {
                currentQrCodePosition = qrCodeDetectorScript.getCurrQrPosition();
                updatePositioningCubePosition();
            } 
            else { // aligned, capture reference POV once and disable QR code detection
                if (!isReferenceImageCaptured) {
                    capturePovImage();
                    isReferenceImageCaptured = true;
                }   
                if (qrDetectionStatus) {
                    qrCodeDetectorScript.OnDisable();
                    qrDetectionStatus = false;
                }
            }

            checkHeadAndPositioningCubeAlignment();
            updateAlignmentVisualizerPositionAndColor();

            if (toggleButtonState) {
                if (replayDataPoint >= storedElapsedTimeArray.Count) { // replay data finished
                    isReplayDataFinished = true;
                }
                else {
                    for (int i = replayDataPoint; i < storedElapsedTimeArray.Count; i++) { // find the best time-matched data for replay
                        if (elapsedMilliseconds == storedElapsedTimeArray[replayDataPoint]) {
                            // var replayData = extract the stored data at the replayDataPoint-th index for replay
                            // currentFrameReplayData.Add(replayData);

                            replayDataPoint = i;
                            break;
                        }
                        else if (elapsedMilliseconds < storedElapsedTimeArray[replayDataPoint]) {
                            // var replayData = extract the stored data at the (replayDataPoint - 1)-th index for replay
                            // currentFrameReplayData.Add(replayData);

                            replayDataPoint = i;
                            break;
                        }
                        else if (elapsedMilliseconds > storedElapsedTimeArray[replayDataPoint]) {
                            replayDataPoint = i;
                            continue;
                        }
                    }
                }
            }
        }

        frameNumber++;
    }

    void OnApplicationPause() {
        batchWriteToLogFile();
        logsDataList = new List<string>();
    }

    private void batchWriteToLogFile() {
        logsDataTw = new StreamWriter(currentModeLogsPath, true);
        for (int i = 0; i < logsDataList.Count; i++)
        {
            logsDataTw.WriteLine(logsDataList[i]);
        }
        logsDataTw.Close();
    }

    public void writeToLogsFile(string logData) {   // HOOK
        int fps = (int)(1.0f / Time.smoothDeltaTime);
        long currentLogsSize = computeLogsSize(logData);

        if (dataCollectionMode) {
             string timeStampedFrameAndQrData = currentDateTime + "," + elapsedMilliseconds.ToString() + "," + frameNumber.ToString() + "," + fps.ToString() + "," + currentQrCodeDistance.ToString() + "," + currentQrCodeDirection.x.ToString() + "," + currentQrCodeDirection.y.ToString() + "," + currentQrCodeDirection.z.ToString() + "," + currentLogsSize.ToString();
            string dataLine = timeStampedFrameAndQrData + "," + logData;
            logsDataList.Add(dataLine);
        }
        else if (dataReplayMode) {
            string timeStampedFrameAndReplayStatusData = currentDateTime + "," + elapsedMilliseconds.ToString() + "," + frameNumber.ToString() + "," + fps.ToString() + "," + currentLogsSize.ToString() + "," + qrDetectionStatus.ToString() + "," + toggleButtonState.ToString();
            string dataLine = timeStampedFrameAndReplayStatusData + "," + logData;
            logsDataList.Add(dataLine);
        }        
    }

    private long computeLogsSize(string logLine) {
        totalLogsSize = totalLogsSize + csvEncoding.GetByteCount(logLine) + newlineBytes;
        return totalLogsSize;
    }

    public Tuple<bool, List<Vector3>> getCurrentFrameData() { // HOOK
        return new Tuple<bool, List<Vector3>>(isReplayDataFinished, currentFrameReplayData);
    }

    private void loadReplayDataFromLogs() { // match the preprocessing steps to the columns in the data collection log file
        if (File.Exists(logsToReplayFilePath)) {
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

                var data_values = line.Split(',');

                // int timestamp = Int32.Parse(data_values[0]);
                int storedElapsedTime = Int32.Parse(data_values[1]);
                // int frameNumber = Int32.Parse(data_values[2]);
                // int fps = Int32.Parse(data_values[3]);
                float qrDistance = float.Parse(data_values[4]);
                Vector3 qrDirection = new Vector3(float.Parse(data_values[5]), float.Parse(data_values[6]), float.Parse(data_values[7]));

                // format data for ordered playback
                storedElapsedTimeArray.Add(storedElapsedTime);

                QRDistanceArray.Enqueue(qrDistance);

                QRDirectionXArray.Add(qrDirection.x);
                QRDirectionYArray.Add(qrDirection.y);
                QRDirectionZArray.Add(qrDirection.z);
            }
        }
        else {
            Debug.Log("EvaluatAR // loadReplayDataFromLogs // DR mode; log file not found while reading data");
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

    public void toggleButtonStateController() { // bind to the toggle button gameobject
        toggleButtonPressCount++;
        toggleButtonState = !toggleButtonState;
    }

    private void updateToggleButtonPosition() {
        toggleButtonRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + toggleButtonOffset;
        toggleButtonRenderer.transform.rotation = MainCamera.transform.rotation;
    }

    private void updatePositioningCubePosition() {
        positioningCubeRenderer.transform.position = currentQrCodePosition - (midDirection * (QRDistanceMid * distanceScalingFactor));
    }

    private void updateAlignmentVisualizerPositionAndColor() {
        alignmentVisualizerRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + alignmentVisualizerOffset;

        if (isPositioned) {
            alignmentVisualizerRenderer.material.color = Color.green;
        }
        else {
            alignmentVisualizerRenderer.material.color = Color.red;
        }
    }

    public void setCurrentCameraCapture(Texture2D currentFrame) { // HOOK; call every frame
        currentCameraCapture = currentFrame;
    }

    private void capturePovImage() {
        currentCameraCapture.Apply();
        File.WriteAllBytes(imagesFilePath, currentCameraCapture.EncodeToPNG());
    }

    private void checkHeadAndPositioningCubeAlignment() {
        if (positioningCubeCollider.bounds.Contains(MainCameraTransformPosition)) {
            if (!isPositioned) {
                isPositioned = true;
            }
        }
    }
}
