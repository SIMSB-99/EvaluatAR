using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.MagicLeap;

using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;

public class BumperOverride : MonoBehaviour
{
    // public variables //

    // private variables //
    private string dirPath = Application.dataPath + "/../SavedImages/";

    private Camera MainCamera;
    private Vector3 MainCameraTransformPosition;
    private MagicLeapInputs _magicLeapInputs;
    private MagicLeapInputs.ControllerActions _controllerActions;
    private int minCaptureWidth = 0;
    private int minCaptureHeight = 0;
    private int captureWidth = 1280;
    private int captureHeight = 720;
    private bool _isCapturing;
    private bool _cameraDeviceAvailable;
    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();
    private MLCamera.Identifier _identifier = MLCamera.Identifier.Main;
    private MLCamera _camera;
    private Texture2D _videoTextureRgb = null;
    private Texture2D _imageTexture;
    private Renderer _screenRendererRGB = null;
    private MLCamera.CaptureConfig _captureConfig;
    private RawImage _screenRendererJPEG = null;

    private LayerMask mask;

    private Size captureSize;
    private Texture2D bboxTexture = null;
    private Mat latestReturnedMatFromModel;

    [SerializeField]
    private GameObject DebugQuad;
    private Renderer DebugQuadRenderer;

    [SerializeField]
    private GameObject FacialDetectionObject;
    private FacialDetection FacialDetctionScript;
    [SerializeField]
    private GameObject FaceCubePrefab;
    private FaceCubeScript FaceCubePrefabScript;

    [SerializeField]
    private TMPro.TextMeshProUGUI FPSTextBox;
    [SerializeField]
    private GameObject Logger;
    private CSVLogger CSVLoggerScript;
    [SerializeField]
    private GameObject EyeTrackerObject;
    private EyeTracking EyeTrackerScript;
    [SerializeField]
    private GameObject AudioCaptureObject;
    private AudioCaptureScript AudioCaptureObjectScript;
    [SerializeField]
    private GameObject ArucoDetectionObject;
    private ArUcoDetector ArucoDetctionScript;

    [SerializeField]
    private GameObject ToggleButton;
    private Renderer ToggleButtonRenderer;
    [SerializeField]
    private Vector3 toggleButtonOffset = new Vector3(-1.5f, -3.5f, 1f);

    [SerializeField] // turns off all logging 
    private bool LoggingMode = true;
    [SerializeField] // false: turns off debug cube position update, debug cube renderer, texture updates 
    private bool DebugCubeMode = true;
    private bool DebugCubeStatus = true; // true: debug cube is enabled so update it's position and FPS display 

    private int FrameCounter = 0;

    private DateTime lastCallToDrawPred = DateTime.Now;

    [SerializeField]
    private int InferenceFrameStep = 1;
    [SerializeField]
    private int LoggingFrameStep = 1;

    private GameObject[] faceCubesInScene;
    private int faceCubeCounter = 1;

    Vector3 direction = new Vector3(0, 0, 0);

    private Stopwatch timer = new Stopwatch();
    private bool toggleButtonStatus = false;

    private Matrix4x4 currCameraTransform = Matrix4x4.identity;
    private uint currIcpWidth = 0;
    private uint currIcpHeight = 0;
    private float currIcpFOV = 0;
    private Vector2 currIcpFocalLength = new Vector2(0, 0);
    private Vector2 currIcpPrincipalPoint = new Vector2(0, 0);
    private float currIcpDistortion0 = 0;
    private float currIcpDistortion1 = 0;
    private float currIcpDistortion2 = 0;
    private float currIcpDistortion3 = 0;
    private float currIcpDistortion4 = 0;

    [SerializeField]
    private bool SaveReferenceImage = false;
    private int savedCounter = 0;
    private Texture2D tempTexture;

    [SerializeField]
    private bool predictPositionMode = false;
    [SerializeField]
    private bool closestDepthMode = false;
    [SerializeField]
    private bool kalmanFilterMode = false;

    [SerializeField]
    private float depthWeight = 2.0f;
    [SerializeField]
    private float twoDDistanceWeight = 0.2f;


    // functions //

    // initialize vairables before application starts
    void Awake()
    {
        // Enabled or disbaled debug logs for whole application
        Debug.unityLogger.logEnabled = LoggingMode;

        MainCamera = Camera.main;

        _isCapturing = false;
        _cameraDeviceAvailable = false;

        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;

        MLPermissions.RequestPermission(MLPermission.Camera, permissionCallbacks);
    }

    // Start is called before the first frame update
    void Start()
    {
        // initialize InputActionAsset, ControllerActions using ML Input
        _magicLeapInputs = new MagicLeapInputs();
        _magicLeapInputs.Enable();
        _controllerActions = new MagicLeapInputs.ControllerActions(_magicLeapInputs);

        // Subscribe to controller events
        _controllerActions.Bumper.performed += HandleOnBumper;

        // get DebugCube's material to switch it on or off
        DebugQuadRenderer = DebugQuad.GetComponent<Renderer>();

        if (!DebugCubeMode)
        {
            DebugQuadRenderer.enabled = false;
            DebugCubeStatus = false;
        }

        captureSize = new Size(captureWidth, captureHeight);

        bboxTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);

        FacialDetctionScript = FacialDetectionObject.GetComponent<FacialDetection>();

        ToggleButtonRenderer = ToggleButton.GetComponent<Renderer>();

        //  CSV Logger Script
        CSVLoggerScript = Logger.GetComponent<CSVLogger>();

        //  face cube script
        FaceCubePrefabScript = FaceCubePrefab.GetComponent<FaceCubeScript>();

        // get eye tracking script in to get eye tracking data
        EyeTrackerScript = EyeTrackerObject.GetComponent<EyeTracking>();

        // get audio capture script
        AudioCaptureObjectScript = AudioCaptureObject.GetComponent<AudioCaptureScript>();

        ArucoDetctionScript = ArucoDetectionObject.GetComponent<ArUcoDetector>();

        // we need collision with only the Face Cubes layer so ray cast only returns face cubes
        mask = LayerMask.GetMask("Face Cubes");
    }

    // Update is called once per frame
    void Update()
    {
        FrameCounter++;

        MainCameraTransformPosition = MainCamera.transform.position;

        faceCubesInScene = GameObject.FindGameObjectsWithTag("FaceCubePrefab");

        if (DebugCubeMode && DebugCubeStatus)
        {
            // always keep DebugCube next to player
            UpdateDebugCubePosition();
        }

        UpdateFaceCubeRotation();

        UpdateButtonsPosition();

        Vector3 currentGazePosition = EyeTrackerScript.getCurrentFixationPoint();

        if (CSVLoggerScript.getReadGazeFromCSV())
        {
            direction = EyeTrackerScript.getCurrentGazeDir();
            MainCameraTransformPosition = EyeTrackerScript.getCurrentGazeOrigin();
        }
        else
        {
            direction = currentGazePosition - MainCameraTransformPosition;
        }

        toggleButtonStatus = EyeTrackerScript.gettoggleButtonState();
        
        // if eye gaze saving mode, then save eye gaze data to CSV file
        if (CSVLoggerScript.getSaveGazePosition())
        {
            // store gaze data only for experiment part 
            if (toggleButtonStatus)
            {
                // get elapsed time for Gaze data for accurate retrival
                // string elapsedTime = timer.ElapsedMilliseconds.ToString();
                string elapsedTime = EyeTrackerScript.getElapsedTimeInMilliseconds().ToString(); 

                // log Gaze Data to CSV
                Vector3 gazePosition = EyeTrackerScript.getCurrentFixationPoint();

                CSVLoggerScript.addGazePositiontoList(elapsedTime, gazePosition);
                CSVLoggerScript.addGazeOriginToList(elapsedTime, MainCameraTransformPosition);
                CSVLoggerScript.addGazeDirToList(elapsedTime, direction);
            }

            // log QR Data to CSV
            string writeDateTime = DateTime.Now.ToString("HH-mm-ss.ffff");

            float QRToCameraDistance = ArucoDetctionScript.getLatestDistance();
            Vector3 QRToCameraDirection = ArucoDetctionScript.getLatestDirection();

            CSVLoggerScript.addQRToCameraDistanceToList(writeDateTime, QRToCameraDistance);
            CSVLoggerScript.addQRToCameraDirectionToList(writeDateTime, QRToCameraDirection);
        }

        if (FrameCounter % LoggingFrameStep == 0)
        {
            // CSV Logger
            FPSCalculator();
        }

        // save reference image too
        if (SaveReferenceImage)
        {
            bool positionedStatus = EyeTrackerScript.getInitiallyPositionedStatus();

            if (positionedStatus && savedCounter == 0)
            {
                string referenceImageFileName = DateTime.Now.ToString("HH-mm-ss.ffff");
                string referenceImagePath = Application.dataPath + "/../SavedImages/" + referenceImageFileName + ".png";

                File.WriteAllBytes(referenceImagePath, tempTexture.EncodeToPNG());
                savedCounter++;
            }
        }

        // drae PredPosition for all Facecubes
        drawPredPos();
    }

    // Handles the disposing all of the input events.
    void OnDestroy()
    {
        _controllerActions.Bumper.performed -= HandleOnBumper;
        _magicLeapInputs.Dispose();

        permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied -= OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain -= OnPermissionDenied;
        _camera = null;
    }

    // Handles the disposing all of the input events.
    void OnDisable()
    {
    }

    private void FPSCalculator()
    {
        int FPS = (int)(1.0f / Time.smoothDeltaTime);
        // Debug.Log("FPS: " + FPS);

        if (DebugCubeMode && DebugCubeStatus)
        {
            FPSTextBox.text = "FPS: " + FPS;
        }

        bool qrDetectionStatus = EyeTrackerScript.getInitiallyPositionedStatus();
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

            Vector3 predictedPosition = faceCubeScript.predictedPosition;
            string faceCubePredictedPosString = "\"Predicted Position\": \"(" + predictedPosition.x + " | " + predictedPosition.y + " | " + predictedPosition.z + ")\" |";

            Vector2 predicted2DPosition = CameraUtilities.ConvertWorldPointToScreen(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, predictedPosition);
            string faceCubePredicted2DPosString = "\"Predicted Position 2D\": \"(" + predicted2DPosition.x + " | " + predicted2DPosition.y + ")\" |";

            double faceCube2DXCenter = faceCubeScript.xCenter;
            double faceCube2DYCenter = faceCubeScript.yCenter;
            string faceCube2DPosString = "\"2D Position\": \"(" + faceCube2DXCenter + " | " + faceCube2DYCenter + ")\" }";

            string faceCubeJSONString = faceCubeName + faceCubeWorldPosString + faceCubeIsSubjectString + faceCubePredictedPosString + faceCubePredicted2DPosString + faceCube2DPosString + " |";

            finalJSONString += faceCubeJSONString;
        }
        finalJSONString += '}';

        // log FPS to CSV
        string writeDateTime = DateTime.Now.ToString("HH-mm-ss.ffff");
        string FPSFileLine = writeDateTime + "," + FPS.ToString() + "," + faceCubesInScene.Length.ToString() + "," + FrameCounter + "," + qrDetectionStatus + "," + toggleButtonStatus + "," + finalJSONString;

        CSVLoggerScript.addFPStoList(FPSFileLine);
    }

    private void drawPredPos()
    {
        for (int i = 0; i < faceCubesInScene.Length; i++)
        {
            var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();
            
            /*
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(faceCubeScript.predictedPosition, 0.05f);
            */
            /*
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = faceCubeScript.predictedPosition;
            marker.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            Renderer markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.material.color = Color.green;
            Destroy(marker, 1.0f);
            */
        }
    }

    // Handles the event for the Bumper.
    private void HandleOnBumper(InputAction.CallbackContext obj)
    {
        if (DebugCubeMode)
        {
            // toggle DebugCube and FPS text on or off
            DebugQuadRenderer.enabled = !DebugQuadRenderer.enabled;
            // keep track of it debug quad is on or off
            DebugCubeStatus = DebugQuadRenderer.enabled;

            FPSTextBox.gameObject.SetActive(DebugQuadRenderer.enabled);
        }
    }

    // Handles updation of DebugCube's position with the MainCamera
    private void UpdateDebugCubePosition()
    {
        DebugQuadRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + (0.25f * Vector3.right);
        DebugQuadRenderer.transform.rotation = MainCamera.transform.rotation;
    }

    private void UpdateButtonsPosition()
    {
        ToggleButtonRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + toggleButtonOffset;
        ToggleButtonRenderer.transform.rotation = MainCamera.transform.rotation;
    }

    // Handles updation of FaceCubes' position with the MainCamera
    private void UpdateFaceCubeRotation()
    {
        for (int i = 0; i < faceCubesInScene.Length; i++)
        {
            faceCubesInScene[i].transform.LookAt(MainCamera.transform);
        }
    }

    private void OnPermissionGranted(string permission)
    {
        StartCoroutine(EnableMLCamera());
    }

    private void OnPermissionDenied(string permission)
    {
        StopCapture();
    }

    // Waits for the camera to be ready and then connects to it.
    private IEnumerator EnableMLCamera()
    {
        // Checks the main camera's availability.
        while (!_cameraDeviceAvailable)
        {
            MLResult result = MLCamera.GetDeviceAvailabilityStatus(_identifier, out _cameraDeviceAvailable);
            if (result.IsOk == false || _cameraDeviceAvailable == false)
            {
                // Wait until camera device is available
                yield return new WaitForSeconds(1.0f);
            }
        }

        // camera device available at this point
        _cameraDeviceAvailable = true;
        ConnectCamera();
    }

    private void ConnectCamera()
    {
        // Once the camera is available, we can connect to it.
        if (_cameraDeviceAvailable)
        {
            MLCamera.ConnectContext connectContext = MLCamera.ConnectContext.Create();
            connectContext.CamId = _identifier;

            connectContext.Flags = MLCamera.ConnectFlag.CamOnly;
            connectContext.EnableVideoStabilization = true;

            _camera = MLCamera.CreateAndConnect(connectContext);

            if (_camera != null)
            {
                Debug.Log("Camera device connected");
                ConfigureCameraInput();
                SetCameraCallbacks();
            }
        }
    }

    private void ConfigureCameraInput()
    {
        // Gets the stream capabilities of the selected camera
        MLCamera.StreamCapability[] streamCapabilities = MLCamera.GetImageStreamCapabilitiesForCamera(_camera, MLCamera.CaptureType.Video);

        if (streamCapabilities.Length == 0)
        {
            return;
        }

        // Set the default capability stream
        MLCamera.StreamCapability defaultCapability = streamCapabilities[0];

        // Try to get the stream that most closely matches the target width and height
        if (MLCamera.TryGetBestFitStreamCapabilityFromCollection(streamCapabilities, captureWidth, captureHeight, MLCamera.CaptureType.Video, out MLCamera.StreamCapability selectedCapability))
        {
            defaultCapability = selectedCapability;
        }

        // Initialize a new capture config
        _captureConfig = new MLCamera.CaptureConfig();

        // Set RGBA video as the output
        MLCamera.OutputFormat outputFormat = MLCamera.OutputFormat.RGBA_8888;

        // Set the Frame Rate to 30fps
        _captureConfig.CaptureFrameRate = MLCamera.CaptureFrameRate._30FPS;

        // Initialize a camera stream config
        _captureConfig.StreamConfigs = new MLCamera.CaptureStreamConfig[1];
        _captureConfig.StreamConfigs[0] = MLCamera.CaptureStreamConfig.Create(defaultCapability, outputFormat);

        StartVideoCapture();
    }

    private void StartVideoCapture()
    {
        MLResult result = _camera.PrepareCapture(_captureConfig, out MLCamera.Metadata metaData);

        if (result.IsOk)
        {
            // Trigger auto exposure and auto white balance
            _camera.PreCaptureAEAWB();

            // Starts video capture
            result = _camera.CaptureVideoStart();
            _isCapturing = MLResult.DidNativeCallSucceed(result.Result, nameof(_camera.CaptureVideoStart));


            if (_isCapturing)
            {
                Debug.Log("Video capture started!");
            }
            else
            {
                Debug.LogError($"Could not start camera capture. Result : {result}");
            }

        }
    }

    private void StopCapture()
    {
        if (_isCapturing)
        {
            _camera.CaptureVideoStop();
        }

        _camera.Disconnect();
        _camera.OnRawVideoFrameAvailable -= RawVideoFrameAvailable;
        _isCapturing = false;
        _cameraDeviceAvailable = false;
    }

    // Assumes that the capture configure was created with a Video CaptureType
    private void SetCameraCallbacks()
    {
        // Provides frames in either YUV/RGBA format depending on the stream configuration
        _camera.OnRawVideoFrameAvailable += RawVideoFrameAvailable;
    }

    private void RawVideoFrameAvailable(MLCamera.CameraOutput output, MLCamera.ResultExtras resultExtras, MLCameraBase.Metadata metadataHandle)
    {
        /*
        if (resultExtras.Intrinsics != null)
        {
            Debug.Log("Width " + resultExtras.Intrinsics.Value.Width);
            Debug.Log("Height " + resultExtras.Intrinsics.Value.Height);
            Debug.Log("FOV " + resultExtras.Intrinsics.Value.FOV);
            Debug.Log("FocalLength " + resultExtras.Intrinsics.Value.FocalLength);
            Debug.Log("PrincipalPoint " + resultExtras.Intrinsics.Value.PrincipalPoint);

            Debug.Log("Width type " + resultExtras.Intrinsics.Value.Width.typeOf());
            Debug.Log("Height type " + resultExtras.Intrinsics.Value.Height.typeOf());
            Debug.Log("FOV type " + resultExtras.Intrinsics.Value.FOV.typeOf());
            Debug.Log("FocalLength type " + resultExtras.Intrinsics.Value.FocalLength.typeOf());
            Debug.Log("PrincipalPoint type " + resultExtras.Intrinsics.Value.PrincipalPoint.typeOf());
        }
        */

        if ((resultExtras.Intrinsics != null) && (MLCVCamera.GetFramePose(resultExtras.VCamTimestamp, out Matrix4x4 cameraTransform).IsOk))
        {
            currCameraTransform = cameraTransform;
            currIcpWidth = resultExtras.Intrinsics.Value.Width;
            currIcpHeight = resultExtras.Intrinsics.Value.Height;
            currIcpFOV = resultExtras.Intrinsics.Value.FOV; 
            currIcpFocalLength = resultExtras.Intrinsics.Value.FocalLength;
            currIcpPrincipalPoint = resultExtras.Intrinsics.Value.PrincipalPoint;

            currIcpDistortion0 = (float)resultExtras.Intrinsics.Value.Distortion[0];
            currIcpDistortion1 = (float)resultExtras.Intrinsics.Value.Distortion[1];
            currIcpDistortion2 = (float)resultExtras.Intrinsics.Value.Distortion[2];
            currIcpDistortion3 = (float)resultExtras.Intrinsics.Value.Distortion[3];
            currIcpDistortion4 = (float)resultExtras.Intrinsics.Value.Distortion[4];
        }
        else
        {
            Debug.Log("GetFramePose API failed");
        }

        if (output.Format == MLCamera.OutputFormat.RGBA_8888)
        {
            //FrameCounter++;

            // Flips the frame vertically so it does not appear upside down.
            MLCamera.FlipFrameVertically(ref output);

            UpdateRGBTexture(ref _videoTextureRgb, output.Planes[0], resultExtras);
            tempTexture = _videoTextureRgb;
            tempTexture.Apply();

        }
    }

    private void UpdateRGBTexture(ref Texture2D videoTextureRGB, MLCamera.PlaneInfo imagePlane, MLCamera.ResultExtras resultExtras)
    {
        if (videoTextureRGB != null && (videoTextureRGB.width != imagePlane.Width || videoTextureRGB.height != imagePlane.Height))
        {
            Destroy(videoTextureRGB);
            videoTextureRGB = null;
        }

        if (videoTextureRGB == null)
        {
            videoTextureRGB = new Texture2D((int)imagePlane.Width, (int)imagePlane.Height, TextureFormat.RGBA32, false);
            videoTextureRGB.filterMode = FilterMode.Bilinear;

            if (DebugCubeMode)
            {
                DebugQuadRenderer.material.mainTexture = bboxTexture;
                // DebugQuadRenderer.material.mainTexture = videoTextureRGB;
            }
        }

        int actualWidth = (int)(imagePlane.Width * imagePlane.PixelStride);

        if (imagePlane.Stride != actualWidth)
        {
            var newTextureChannel = new byte[actualWidth * imagePlane.Height];
            for (int i = 0; i < imagePlane.Height; i++)
            {
                Buffer.BlockCopy(imagePlane.Data, (int)(i * imagePlane.Stride), newTextureChannel, i * actualWidth, actualWidth);
            }

            if (DebugCubeMode)
            {
                videoTextureRGB.LoadRawTextureData(newTextureChannel);
            }
        }
        else
        {
            videoTextureRGB.LoadRawTextureData(imagePlane.Data);
            // videoTextureRGB.Apply();

            // Tuple<List<Vector2>, List<drawPredInfo>>
            var pixelFaceCentersAndDrawPreds = Predict(videoTextureRGB, resultExtras);

            // render detected faces' bboxes as cubes in world coordinates
            faceCubeInWorldVisualization(resultExtras, pixelFaceCentersAndDrawPreds.Item1, pixelFaceCentersAndDrawPreds.Item2);

            // keep checking which face cube (if any) is eye gaze interacting with
            ThresholdingLogicForFaceCube();

            // get the face cubes of bystanders (isSubject == false) and draw bbox for them on debug cube texture
            DrawBystanderBboxes();
        }
    }

    private Tuple<List<Vector2>, List<drawPredInfo>> Predict(Texture2D videoTextureRGB, MLCamera.ResultExtras resultExtras)
    {
        // Tuple<Mat, List<Vector2>, List<drawPredInfo>>
        if (FrameCounter % InferenceFrameStep == 0)
        {
            var detectionFrameCentersAndDrawPredInfo = FacialDetctionScript.RunDetection(videoTextureRGB);

            latestReturnedMatFromModel = detectionFrameCentersAndDrawPredInfo.Item1;

            return new Tuple<List<Vector2>, List<drawPredInfo>>(detectionFrameCentersAndDrawPredInfo.Item2, detectionFrameCentersAndDrawPredInfo.Item3);
        }
        else
        {
            latestReturnedMatFromModel = FacialDetctionScript.MatFromTexture(videoTextureRGB);

            // update 2D coords of each rendered face cube using its 3D position for every non-inference frame
            worldToPixelConversion(resultExtras);

            List<Vector2> listOfVector2 = new List<Vector2>();
            List<drawPredInfo> drawPredInfoList = new List<drawPredInfo>();
            return new Tuple<List<Vector2>, List<drawPredInfo>>(listOfVector2, drawPredInfoList);
        }
    }

    private void worldToPixelConversion(MLCamera.ResultExtras resultExtras)
    {
        for (int i = 0; i < faceCubesInScene.Length; i++)
        {
            Vector3 currWorldCoords = faceCubesInScene[i].transform.position;

            /*
            if ((resultExtras.Intrinsics != null) && (MLCVCamera.GetFramePose(resultExtras.VCamTimestamp, out Matrix4x4 cameraTransform).IsOk))
            {
                Debug.Log("GetFramePose and resultExtras.Intrinsics both pass in WorldToPixelConversion function");

                var pixelPosition = CameraUtilities.ConvertWorldPointToScreen(resultExtras.Intrinsics.Value, cameraTransform, currWorldCoords);

                // store the latest 2D coords to be used to draw preds on texture
                var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

                double left = pixelPosition.x - (faceCubeScript.detection_width / 2.0);
                double top = pixelPosition.y + (faceCubeScript.detection_height / 2.0);
                double right = pixelPosition.x + (faceCubeScript.detection_width / 2.0);
                double bottom = pixelPosition.y - (faceCubeScript.detection_height / 2.0);

                // TO DO: Clean this code up by using Math.Clamp (or something similar)
                if (left < (double)minCaptureWidth)
                {
                    faceCubeScript.l = (double)minCaptureWidth;
                }
                else
                {
                    faceCubeScript.l = left;
                }

                if (top > (double)captureHeight)
                {
                    faceCubeScript.t = (double)captureHeight;
                }
                else
                {
                    faceCubeScript.t = top;
                }

                if (right > (double)captureWidth)
                {
                    faceCubeScript.r = (double)captureWidth;
                }
                else
                {
                    faceCubeScript.r = right;
                }

                if (bottom < (double)minCaptureHeight)
                {
                    faceCubeScript.b = (double)minCaptureHeight;
                }
                else
                {
                    faceCubeScript.b = bottom;
                }
            }
            */

            if (currCameraTransform != Matrix4x4.identity && currIcpWidth != 0 && currIcpHeight != 0 && currIcpFOV != 0 && currIcpFocalLength != Vector2.zero && currIcpPrincipalPoint != Vector2.zero)
            {
                // var pixelPosition = CameraUtilities.ConvertWorldPointToScreen(currIcp, currCameraTransform, currWorldCoords);
                var pixelPosition = CameraUtilities.ConvertWorldPointToScreen(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, currWorldCoords);

                // store the latest 2D coords to be used to draw preds on texture
                var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

                double left = pixelPosition.x - (faceCubeScript.detection_width / 2.0);
                double top = pixelPosition.y + (faceCubeScript.detection_height / 2.0);
                double right = pixelPosition.x + (faceCubeScript.detection_width / 2.0);
                double bottom = pixelPosition.y - (faceCubeScript.detection_height / 2.0);

                // TO DO: Clean this code up by using Math.Clamp (or something similar)
                if (left < (double)minCaptureWidth)
                {
                    faceCubeScript.l = (double)minCaptureWidth;
                }
                else
                {
                    faceCubeScript.l = left;
                }

                if (top > (double)captureHeight)
                {
                    faceCubeScript.t = (double)captureHeight;
                }
                else
                {
                    faceCubeScript.t = top;
                }

                if (right > (double)captureWidth)
                {
                    faceCubeScript.r = (double)captureWidth;
                }
                else
                {
                    faceCubeScript.r = right;
                }

                if (bottom < (double)minCaptureHeight)
                {
                    faceCubeScript.b = (double)minCaptureHeight;
                }
                else
                {
                    faceCubeScript.b = bottom;
                }
            }
        }
    }

    private void faceCubeInWorldVisualization(MLCamera.ResultExtras resultExtras, List<Vector2> pixelFaceCentersList, List<drawPredInfo> drawPredInfoList)
    {
        List<string> assignedFaceCubeNames = new List<string>();

        if (predictPositionMode && closestDepthMode)
        {
            if (currCameraTransform != Matrix4x4.identity && currIcpWidth != 0 && currIcpHeight != 0 && currIcpFOV != 0 && currIcpFocalLength != Vector2.zero && currIcpPrincipalPoint != Vector2.zero)
            {
                Debug.Log("frame number: " + FrameCounter);

                for (int i = 0; i < pixelFaceCentersList.Count; i++)
                {
                    // new 3D position for the detected face
                    var resultPosition = CameraUtilities.CastRayFromScreenToWorldPoint(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, pixelFaceCentersList[i], currIcpDistortion0, currIcpDistortion1, currIcpDistortion2, currIcpDistortion3, currIcpDistortion4);
                    
                    var overlapBoxes = Physics.OverlapBox(resultPosition, FaceCubePrefab.transform.localScale / 2, Quaternion.identity, mask);
                    if (overlapBoxes.Length > 0)
                    {
                        if (kalmanFilterMode)
                        {
                            int bestMatchIndex = -1;
                            float minCost = float.MaxValue;

                            // calculate the distance of this face's new 3D position with predicted position and predicted depth of all overlapping face cubes
                            // this will assign best match score to all face cubes in scene that are best candidates for the current face
                            for (int j = 0; j < overlapBoxes.Length; j++)
                            {
                                // check if the curr overlapped face cube has already been assigned to some other detection 
                                if (assignedFaceCubeNames.Contains(overlapBoxes[j].gameObject.name))
                                {
                                    Debug.Log("FaceCube already assigned to some other detection");
                                    continue;
                                }

                                var faceCubeScript = overlapBoxes[j].GetComponent<FaceCubeScript>();

                                Vector3 predictedPosition = faceCubeScript.kalmanPredictedPosition();

                                // float distanceCost = Vector3.Distance(resultPosition, predictedPosition);
                                float distanceCost = Vector2.Distance(new Vector2(resultPosition.x, resultPosition.y), new Vector2(predictedPosition.x, predictedPosition.y)); // this is 2D distance cost between x and y axes
                                float depthCost = Mathf.Abs(resultPosition.z - predictedPosition.z);
                                // float totalCost = distanceCost + depthCost;
                                float totalCost = (depthWeight * depthCost) + (twoDDistanceWeight * distanceCost);

                                if (totalCost < minCost)
                                {
                                    minCost = totalCost;
                                    bestMatchIndex = j;
                                }

                                Debug.Log("detected face (i): " + i + " 3D pos of detected face: " + resultPosition + " overlapped(j) currentPosition: " + faceCubeScript.currentPosition + " overlapped(j) predictedPosition: " + predictedPosition + " distance with overlapped(j):: " + distanceCost + " depth with overlapped(j):: " + depthCost + " distance with overlapped(j): " + distanceCost + " totalCost with overlapped(j): " + totalCost);
                            }

                            // if best matching face cube for the current detected face is found and threshold is met then associate it
                            if (bestMatchIndex != -1) // && minCost < 0.5f)
                            {
                                Debug.Log("Match picked is overlapped (j): " + bestMatchIndex);

                                //  stored the assigned face cube to the list
                                assignedFaceCubeNames.Add(overlapBoxes[bestMatchIndex].gameObject.name);

                                var overlappedBestMatchGameObject = overlapBoxes[bestMatchIndex].gameObject;
                                var faceCubeScript = overlapBoxes[bestMatchIndex].GetComponent<FaceCubeScript>();

                                overlapBoxes[bestMatchIndex].gameObject.transform.position = resultPosition;

                                faceCubeScript.updateKalmanState(resultPosition);

                                // update it's drawPred details as well
                                faceCubeScript.setStaleCounter();

                                faceCubeScript.cid = drawPredInfoList[i].classId;
                                faceCubeScript.c = drawPredInfoList[i].conf;
                                faceCubeScript.l = drawPredInfoList[i].left;
                                faceCubeScript.t = drawPredInfoList[i].top;
                                faceCubeScript.r = drawPredInfoList[i].right;
                                faceCubeScript.b = drawPredInfoList[i].bottom;

                                faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                                faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                                faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                                faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                            }
                            else
                            {
                                Debug.Log("no best match face cube found. for testing not initializing a new one too");
                            }
                        }
                        else
                        {
                            // naive/custom implementation for calculating predicted position
                            int bestMatchIndex = -1;
                            float minCost = float.MaxValue;

                            // calculate the distance of this face's new 3D position with predicted position and predicted depth of all overlapping face cubes
                            // this will assign best match score to all face cubes in scene that are best candidates for the current face
                            for (int j = 0; j < overlapBoxes.Length; j++)
                            {
                                // check if the curr overlapped face cube has already been assigned to some other detection 
                                if (assignedFaceCubeNames.Contains(overlapBoxes[j].gameObject.name))
                                {
                                    Debug.Log("FaceCube already assigned to some other detection");
                                    continue;
                                }

                                var faceCubeScript = overlapBoxes[j].GetComponent<FaceCubeScript>();

                                Vector3 predictedPosition = faceCubeScript.calculatePredictedPosition();

                                // float distanceCost = Vector3.Distance(resultPosition, predictedPosition);
                                float distanceCost = Vector2.Distance(new Vector2(resultPosition.x, resultPosition.y), new Vector2(predictedPosition.x, predictedPosition.y)); // this is 2D distance cost between x and y axes
                                float depthCost = Mathf.Abs(resultPosition.z - faceCubeScript.predictedDepth);
                                // float totalCost = distanceCost + depthCost;
                                float totalCost = (depthWeight * depthCost) + (twoDDistanceWeight * distanceCost);

                                if (totalCost < minCost)
                                {
                                    minCost = totalCost;
                                    bestMatchIndex = j;
                                }

                                Debug.Log("detected face (i): " + i + " 3D pos of detected face: " + resultPosition + " overlapped(j) currentPosition: " + faceCubeScript.currentPosition + " overlapped(j) predictedPosition: " + predictedPosition + " distance with overlapped(j):: " + distanceCost + " depth with overlapped(j):: " + depthCost + " distance with overlapped(j): " + distanceCost + " totalCost with overlapped(j): " + totalCost);
                            }

                            // if best matching face cube for the current detected face is found and threshold is met then associate it
                            if (bestMatchIndex != -1) // && minCost < 0.5f)
                            {
                                Debug.Log("Match picked is overlapped (j): " + bestMatchIndex);

                                //  stored the assigned face cube to the list
                                assignedFaceCubeNames.Add(overlapBoxes[bestMatchIndex].gameObject.name);

                                var overlappedBestMatchGameObject = overlapBoxes[bestMatchIndex].gameObject;
                                var faceCubeScript = overlapBoxes[bestMatchIndex].GetComponent<FaceCubeScript>();

                                overlapBoxes[bestMatchIndex].gameObject.transform.position = resultPosition;

                                faceCubeScript.updateVelocityDepthCurrentAndHistoryPositions(resultPosition);

                                // update it's drawPred details as well
                                faceCubeScript.setStaleCounter();

                                faceCubeScript.cid = drawPredInfoList[i].classId;
                                faceCubeScript.c = drawPredInfoList[i].conf;
                                faceCubeScript.l = drawPredInfoList[i].left;
                                faceCubeScript.t = drawPredInfoList[i].top;
                                faceCubeScript.r = drawPredInfoList[i].right;
                                faceCubeScript.b = drawPredInfoList[i].bottom;

                                faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                                faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                                faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                                faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                            }
                            else
                            {
                                Debug.Log("no best match face cube found. for testing not initializing a new one too");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("there was no overlap. initializing a new one");

                        // else instantiate a new face cube to associate with this face
                        var newObject = Instantiate(FaceCubePrefab, resultPosition, Quaternion.identity);
                        newObject.name = "FaceCube-" + faceCubeCounter.ToString();
                        faceCubeCounter++;

                        var faceCubeScript = newObject.GetComponent<FaceCubeScript>();

                        // update it's drawPred details as well
                        faceCubeScript.cid = drawPredInfoList[i].classId;
                        faceCubeScript.c = drawPredInfoList[i].conf;
                        faceCubeScript.l = drawPredInfoList[i].left;
                        faceCubeScript.t = drawPredInfoList[i].top;
                        faceCubeScript.r = drawPredInfoList[i].right;
                        faceCubeScript.b = drawPredInfoList[i].bottom;

                        faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                        faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                        faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                        faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                    }
                }
            }
        }
        else if (predictPositionMode)
        {
            if (currCameraTransform != Matrix4x4.identity && currIcpWidth != 0 && currIcpHeight != 0 && currIcpFOV != 0 && currIcpFocalLength != Vector2.zero && currIcpPrincipalPoint != Vector2.zero)
            {
                Debug.Log("frame number: " + FrameCounter);

                for (int i = 0; i < pixelFaceCentersList.Count; i++)
                {
                    var resultPosition = CameraUtilities.CastRayFromScreenToWorldPoint(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, pixelFaceCentersList[i], currIcpDistortion0, currIcpDistortion1, currIcpDistortion2, currIcpDistortion3, currIcpDistortion4);

                    var overlapBoxes = Physics.OverlapBox(resultPosition, FaceCubePrefab.transform.localScale / 2, Quaternion.identity, mask);
                    if (overlapBoxes.Length > 0)
                    {
                        if (kalmanFilterMode)
                        {
                            Debug.Log("Predicted Position with Kalman Filters Mode");

                            float minDistance = float.MaxValue;
                            int minDistIndex = -1;

                            for (int j = 0; j < overlapBoxes.Length; j++)
                            {
                                // check if the curr overlapped face cube has already been assigned to some other detection 
                                if (assignedFaceCubeNames.Contains(overlapBoxes[j].gameObject.name))
                                {
                                    Debug.Log("FaceCube already assigned to some other detection");
                                    continue;
                                }

                                var faceCubeScript = overlapBoxes[j].gameObject.GetComponent<FaceCubeScript>();

                                Vector3 predictedPosition = faceCubeScript.kalmanPredictedPosition();
                                // float distanceCost = Vector3.Distance(resultPosition, predictedPosition);
                                float distance = Vector2.Distance(new Vector2(resultPosition.x, resultPosition.y), new Vector2(predictedPosition.x, predictedPosition.y)); // this is 2D distance cost between x and y axes

                                if (distance < minDistance)
                                {
                                    // Debug.Log("This is the closest distance");
                                    minDistance = distance;
                                    minDistIndex = j;
                                }
                            }

                            // if best matching face cube for the current detected face is found
                            if (minDistIndex != -1)
                            {
                                Debug.Log("Match picked is overlapped (j): " + minDistIndex);

                                //  stored the assigned face cube to the list
                                assignedFaceCubeNames.Add(overlapBoxes[minDistIndex].gameObject.name);

                                var overlappedBestMatchGameObject = overlapBoxes[minDistIndex].gameObject;
                                var faceCubeScript = overlapBoxes[minDistIndex].GetComponent<FaceCubeScript>();

                                overlapBoxes[minDistIndex].gameObject.transform.position = resultPosition;

                                faceCubeScript.updateKalmanState(resultPosition);

                                // update it's drawPred details as well
                                faceCubeScript.setStaleCounter();

                                faceCubeScript.cid = drawPredInfoList[i].classId;
                                faceCubeScript.c = drawPredInfoList[i].conf;
                                faceCubeScript.l = drawPredInfoList[i].left;
                                faceCubeScript.t = drawPredInfoList[i].top;
                                faceCubeScript.r = drawPredInfoList[i].right;
                                faceCubeScript.b = drawPredInfoList[i].bottom;

                                faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                                faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                                faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                                faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                            }
                            else
                            {
                                Debug.Log("no best match face cube found. for testing not initializing a new one too");
                            }
                        }
                        else
                        {
                            // instead of a naive overlap
                            // check which overlapped faceCube has the smallest predicted distance from it

                            Debug.Log("Predicted Position with naive implementation Mode");
                             
                            float minDistance = float.MaxValue;
                            int minDistIndex = 0;

                            for (int j = 0; j < overlapBoxes.Length; j++)
                            {
                                // check if the curr overlapped face cube has already been assigned to some other detection 
                                if (assignedFaceCubeNames.Contains(overlapBoxes[j].gameObject.name))
                                {
                                    Debug.Log("FaceCube already assigned to some other detection");
                                    continue;
                                }

                                var overlapFaceCubeScript = overlapBoxes[j].gameObject.GetComponent<FaceCubeScript>();

                                Vector3 faceCubePrevPos = overlapFaceCubeScript.previousPosition;
                                Vector3 faceCubeCurrPos = overlapFaceCubeScript.currentPosition;
                                Vector3 faceCubePredPos = overlapFaceCubeScript.predictedPosition;

                                if ((faceCubePrevPos != Vector3.zero) && (faceCubePredPos != faceCubeCurrPos))
                                {
                                    // all positions for the faceCube are valid; has valid predicted position
                                    float distance = Vector3.Distance(faceCubePredPos, faceCubeCurrPos);

                                    // Debug.Log("Distance " + distance + " with predicted position of overlapped faceCube: " + overlapFaceCubeScript.faceName);

                                    if (distance < minDistance)
                                    {
                                        // Debug.Log("This is the closest distance");
                                        minDistance = distance;
                                        minDistIndex = j;
                                    }
                                }
                            }

                            //  stored the assigned face cube to the list
                            assignedFaceCubeNames.Add(overlapBoxes[minDistIndex].gameObject.name);

                            var faceCubeScript = overlapBoxes[minDistIndex].gameObject.GetComponent<FaceCubeScript>();
                            faceCubeScript.setStaleCounter();

                            // update it's drawPred details as well
                            faceCubeScript.cid = drawPredInfoList[i].classId;
                            faceCubeScript.c = drawPredInfoList[i].conf;
                            faceCubeScript.l = drawPredInfoList[i].left;
                            faceCubeScript.t = drawPredInfoList[i].top;
                            faceCubeScript.r = drawPredInfoList[i].right;
                            faceCubeScript.b = drawPredInfoList[i].bottom;

                            faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                            faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                            faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                            faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;

                            faceCubeScript.previousPosition = faceCubeScript.currentPosition;
                            faceCubeScript.currentPosition = resultPosition;

                            overlapBoxes[minDistIndex].gameObject.transform.position = resultPosition;
                        }    
                    }
                    else
                    {
                        var newObject = Instantiate(FaceCubePrefab, resultPosition, Quaternion.identity);
                        newObject.name = "FaceCube-" + faceCubeCounter.ToString();
                        faceCubeCounter++;

                        var faceCubeScript = newObject.GetComponent<FaceCubeScript>();

                        // update it's drawPred details as well
                        faceCubeScript.cid = drawPredInfoList[i].classId;
                        faceCubeScript.c = drawPredInfoList[i].conf;
                        faceCubeScript.l = drawPredInfoList[i].left;
                        faceCubeScript.t = drawPredInfoList[i].top;
                        faceCubeScript.r = drawPredInfoList[i].right;
                        faceCubeScript.b = drawPredInfoList[i].bottom;

                        faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                        faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                        faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                        faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                    }
                }
            }
        }
        else if (closestDepthMode)
        {
            Debug.Log("closestDepthMode");

            if (currCameraTransform != Matrix4x4.identity && currIcpWidth != 0 && currIcpHeight != 0 && currIcpFOV != 0 && currIcpFocalLength != Vector2.zero && currIcpPrincipalPoint != Vector2.zero)
            {
                Debug.Log("frame number: " + FrameCounter);

                for (int i = 0; i < pixelFaceCentersList.Count; i++)
                {
                    Debug.Log("Detected face number: " + i);

                    var resultPosition = CameraUtilities.CastRayFromScreenToWorldPoint(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, pixelFaceCentersList[i], currIcpDistortion0, currIcpDistortion1, currIcpDistortion2, currIcpDistortion3, currIcpDistortion4);

                    var overlapBoxes = Physics.OverlapBox(resultPosition, FaceCubePrefab.transform.localScale / 2, Quaternion.identity, mask);
                    if (overlapBoxes.Length > 0)
                    {
                        // instead of a naive overlap
                        // check which overlapped faceCube has the smallest distance on the z-axis
                        float minDistance = float.MaxValue;
                        int minDistIndex = 0;

                        for (int j = 0; j < overlapBoxes.Length; j++)
                        {
                            // check if the curr overlapped face cube has already been assigned to some other detection 
                            if (assignedFaceCubeNames.Contains(overlapBoxes[j].gameObject.name))
                            {
                                Debug.Log("FaceCube already assigned to some other detection");
                                continue;
                            }

                            var currOverlappedGameObjectPos = overlapBoxes[j].gameObject.transform.position;

                            float zDistance = (resultPosition - currOverlappedGameObjectPos).z;
                            if (zDistance < minDistance)
                            {
                                // Debug.Log("This is the closest distance");
                                minDistance = zDistance;
                                minDistIndex = j;
                            }
                        }

                        //  stored the assigned face cube to the list
                        assignedFaceCubeNames.Add(overlapBoxes[minDistIndex].gameObject.name);

                        var faceCubeScript = overlapBoxes[minDistIndex].gameObject.GetComponent<FaceCubeScript>();
                        faceCubeScript.setStaleCounter();

                        // update it's drawPred details as well
                        faceCubeScript.cid = drawPredInfoList[i].classId;
                        faceCubeScript.c = drawPredInfoList[i].conf;
                        faceCubeScript.l = drawPredInfoList[i].left;
                        faceCubeScript.t = drawPredInfoList[i].top;
                        faceCubeScript.r = drawPredInfoList[i].right;
                        faceCubeScript.b = drawPredInfoList[i].bottom;

                        faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                        faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                        faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                        faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;

                        overlapBoxes[minDistIndex].gameObject.transform.position = resultPosition;
                    }
                    else
                    {
                        var newObject = Instantiate(FaceCubePrefab, resultPosition, Quaternion.identity);
                        newObject.name = "FaceCube-" + faceCubeCounter.ToString();
                        faceCubeCounter++;

                        var faceCubeScript = newObject.GetComponent<FaceCubeScript>();

                        // update it's drawPred details as well
                        faceCubeScript.cid = drawPredInfoList[i].classId;
                        faceCubeScript.c = drawPredInfoList[i].conf;
                        faceCubeScript.l = drawPredInfoList[i].left;
                        faceCubeScript.t = drawPredInfoList[i].top;
                        faceCubeScript.r = drawPredInfoList[i].right;
                        faceCubeScript.b = drawPredInfoList[i].bottom;

                        faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                        faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                        faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                        faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                    }
                }
            }
        }
        else
        {
            // Old naive overlapping ALG (picks the first [0th] overlapping face cube

            if (currCameraTransform != Matrix4x4.identity && currIcpWidth != 0 && currIcpHeight != 0 && currIcpFOV != 0 && currIcpFocalLength != Vector2.zero && currIcpPrincipalPoint != Vector2.zero)
            {
                for (int i = 0; i < pixelFaceCentersList.Count; i++)
                {
                    // var resultPosition = CameraUtilities.CastRayFromScreenToWorldPoint(currIcp, currCameraTransform, pixelFaceCentersList[i]);
                    var resultPosition = CameraUtilities.CastRayFromScreenToWorldPoint(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, pixelFaceCentersList[i], currIcpDistortion0, currIcpDistortion1, currIcpDistortion2, currIcpDistortion3, currIcpDistortion4);
                    var overlapBoxes = Physics.OverlapBox(resultPosition, FaceCubePrefab.transform.localScale / 2, Quaternion.identity, mask);

                    if (overlapBoxes.Length > 0)
                    {
                        overlapBoxes[0].gameObject.transform.position = resultPosition;

                        var faceCubeScript = overlapBoxes[0].gameObject.GetComponent<FaceCubeScript>();
                        faceCubeScript.setStaleCounter();

                        // update it's drawPred details as well
                        faceCubeScript.cid = drawPredInfoList[i].classId;
                        faceCubeScript.c = drawPredInfoList[i].conf;
                        faceCubeScript.l = drawPredInfoList[i].left;
                        faceCubeScript.t = drawPredInfoList[i].top;
                        faceCubeScript.r = drawPredInfoList[i].right;
                        faceCubeScript.b = drawPredInfoList[i].bottom;

                        faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                        faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                        faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                        faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                    }
                    else
                    {
                        var newObject = Instantiate(FaceCubePrefab, resultPosition, Quaternion.identity);
                        newObject.name = "FaceCube-" + faceCubeCounter.ToString();
                        faceCubeCounter++;

                        var faceCubeScript = newObject.GetComponent<FaceCubeScript>();

                        // update it's drawPred details as well
                        faceCubeScript.cid = drawPredInfoList[i].classId;
                        faceCubeScript.c = drawPredInfoList[i].conf;
                        faceCubeScript.l = drawPredInfoList[i].left;
                        faceCubeScript.t = drawPredInfoList[i].top;
                        faceCubeScript.r = drawPredInfoList[i].right;
                        faceCubeScript.b = drawPredInfoList[i].bottom;

                        faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
                        faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

                        faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
                        faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
                    }
                }
            }
        }

    }

    private void ThresholdingLogicForFaceCube()
    {
        // check the face cube that eye gaze is colliding with and pick the first one
        if (Physics.Raycast(MainCameraTransformPosition, direction, out RaycastHit hit, 100, mask))
        {
            GameObject targetFaceCube = hit.transform.gameObject;
            var targetFaceCubeScript = hit.transform.gameObject.GetComponent<FaceCubeScript>();

            if (targetFaceCubeScript.getIslooking() == false)
            {
                targetFaceCubeScript.setIslooking(true);
                targetFaceCubeScript.EyeContactStarted();
            }
            else
            {
                targetFaceCubeScript.setIslooking(true);
                targetFaceCubeScript.EyeContactMaintained();
            }

            // isLooking for all other facecube's should be false
            for (int i = 0; i < faceCubesInScene.Length; i++)
            {
                if (faceCubesInScene[i].name != targetFaceCube.name)
                {
                    var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

                    if (faceCubeScript.getIslooking() == true)
                    {
                        faceCubeScript.EyeContactLost();
                        faceCubeScript.setIslooking(false);
                    }
                }
            }

            // check if talking while looking at this face cube as well
            bool currentlyIsTalking = AudioCaptureObjectScript.getIsTalking();
            targetFaceCubeScript.setIsTalking(currentlyIsTalking);
        }
        else
        {
            // isLooking for all facecube's should be false
            for (int i = 0; i < faceCubesInScene.Length; i++)
            {
                var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();
                if (faceCubeScript.getIslooking() == true)
                {
                    faceCubeScript.EyeContactLost();
                    faceCubeScript.setIslooking(false);
                }
            }
        }
    }

    private void DrawBystanderBboxes()
    {
        //  check if there are any detections or not, if not then apply the texture as it is
        if (faceCubesInScene.Length == 0)
        {
            if (DebugCubeMode && DebugCubeStatus)
            {
                Utils.matToTexture2D(latestReturnedMatFromModel, bboxTexture);

                bboxTexture.Apply();
            }
        }
        else
        {
            Mat matWithDetections = latestReturnedMatFromModel;

            for (int i = 0; i < faceCubesInScene.Length; i++)
            {
                // get facecube's script
                var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

                // check if face cube is of bystander - ignore if it is a subject
                if (faceCubeScript.getIsSubject() == true)
                {
                    continue;
                }

                // for bystanders get the 2D coorindates and call drawPreds to make bboxes on texture
                matWithDetections = FacialDetctionScript.drawPred(faceCubeScript.cid, faceCubeScript.c, faceCubeScript.l, faceCubeScript.t, faceCubeScript.r, faceCubeScript.b, latestReturnedMatFromModel);
            }

            if (DebugCubeMode && DebugCubeStatus)
            {
                Utils.matToTexture2D(matWithDetections, bboxTexture);

                bboxTexture.Apply();
            }

        }
    }
}