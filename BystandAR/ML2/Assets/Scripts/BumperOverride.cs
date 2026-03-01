using System;
using System.IO;
using System.Linq;
using System.Text;
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

    //private LayerMask mask;

    private Size captureSize;
    private Texture2D bboxTexture = null;


    [SerializeField] private EvaluatAR evaluatAR;
    [SerializeField] private FacialDetection facialDetection;
    [SerializeField] private EyeTracking eyeTracking;
    [SerializeField] private ArUcoDetector arucoDetector;

    [SerializeField, Tooltip("If true, this script will push per-frame (or downsampled) logs into EvaluatAR.writeToLogsFile().")]
    private bool enableEvaluatARLogging = true;

    [SerializeField, Min(1), Tooltip("Write one EvaluatAR log entry every N Update() frames.")]
    private int logEveryNFrames = 1;

    [SerializeField, Min(1), Tooltip("Run OpenCV face inference every N logged frames (the last detections are re-used in between).")]
    private int faceInferenceEveryNLoggedFrames = 1;

    private int _logFrameCounter = 0;
    private bool _hasNewVideoFrame = false;

    private static string Vec3Pipe(Vector3 v) =>
        "(" + v.x.ToString("F4") + " | " + v.y.ToString("F4") + " | " + v.z.ToString("F4") + ")";

    private static string QuatPipe(Quaternion q) =>
        "(" + q.x.ToString("F4") + " | " + q.y.ToString("F4") + " | " + q.z.ToString("F4") + " | " + q.w.ToString("F4") + ")";

    //private Mat latestReturnedMatFromModel;

    [SerializeField]
    private GameObject DebugQuad;
    private Renderer DebugQuadRenderer;

    [SerializeField] // turns off all logging 
    private bool LoggingMode = true;
    [SerializeField] // false: turns off debug cube position update, debug cube renderer, texture updates 
    private bool DebugCubeMode = true;
    private bool DebugCubeStatus = true; // true: debug cube is enabled so update it's position and FPS display 

    Vector3 direction = new Vector3(0, 0, 0);

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

    private Texture2D tempTexture;

    [SerializeField]
    private bool predictPositionMode = false;
    [SerializeField]
    private bool closestDepthMode = false;
    [SerializeField]
    private bool kalmanFilterMode = false;



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

        //FacialDetctionScript = FacialDetectionObject.GetComponent<FacialDetection>();

        //ToggleButtonRenderer = ToggleButton.GetComponent<Renderer>();

        ////  CSV Logger Script
        //CSVLoggerScript = Logger.GetComponent<CSVLogger>();

        //  face cube script
        //FaceCubePrefabScript = FaceCubePrefab.GetComponent<FaceCubeScript>();

        //// get eye tracking script in to get eye tracking data
        //EyeTrackerScript = EyeTrackerObject.GetComponent<EyeTracking>();

        //// get audio capture script
        //AudioCaptureObjectScript = AudioCaptureObject.GetComponent<AudioCaptureScript>();

        //ArucoDetctionScript = ArucoDetectionObject.GetComponent<ArUcoDetector>();

        //// we need collision with only the Face Cubes layer so ray cast only returns face cubes
        //mask = LayerMask.GetMask("Face Cubes");
    }

    // Update is called once per frame
    void Update()
    {
        //FrameCounter++;

        MainCameraTransformPosition = MainCamera.transform.position;

        //faceCubesInScene = GameObject.FindGameObjectsWithTag("FaceCubePrefab");

        if (DebugCubeMode && DebugCubeStatus)
        {
            // always keep DebugCube next to player
            UpdateDebugCubePosition();
        }

        if (enableEvaluatARLogging && evaluatAR != null)
        {
            if (_hasNewVideoFrame && tempTexture != null)
            {
                evaluatAR.setCurrentCameraCapture(tempTexture);
                _hasNewVideoFrame = false;
            }

            _logFrameCounter++;
            int logStep = Mathf.Max(1, logEveryNFrames);

            if ((_logFrameCounter % logStep) == 0 && tempTexture != null)
            {
                int inferStep = Mathf.Max(1, faceInferenceEveryNLoggedFrames);
                if (facialDetection != null && ((_logFrameCounter / logStep) % inferStep) == 0)
                {
                    facialDetection.RunDetection(tempTexture);
                }

                bool hardcodedEyeGaze = (eyeTracking != null) ? eyeTracking.getReadGazeFromCSVStatus() : false;
                bool toggleButtonState = (eyeTracking != null) ? eyeTracking.gettoggleButtonState() : false;

                bool isAligned = (eyeTracking != null) ? eyeTracking.getInitiallyPositionedStatus() : false;
                bool qrDetectionStatus = !isAligned;

                Vector3 fixation = (eyeTracking != null) ? eyeTracking.getCurrentFixationPoint() : Vector3.zero;
                Vector3 gazeOrigin = (eyeTracking != null) ? eyeTracking.getCurrentGazeOrigin() : Vector3.zero;
                Vector3 gazeDir = (eyeTracking != null) ? eyeTracking.getCurrentGazeDir() : Vector3.zero;

                float qrDistance = (arucoDetector != null) ? arucoDetector.getLatestDistance() : 0.0f;
                Vector3 qrDirection = (arucoDetector != null) ? arucoDetector.getLatestDirection() : Vector3.zero;
                Vector3 qrPosition = (arucoDetector != null) ? arucoDetector.getLatestPosition() : Vector3.zero;
                Quaternion qrRotation = (arucoDetector != null) ? arucoDetector.getLatestRotation() : Quaternion.identity;

                string statusPipe =
                    "{\"type\":\"status\" | \"qrDetectionStatus\":\"" + qrDetectionStatus +
                    "\" | \"hardcodedEyeGaze\":\"" + hardcodedEyeGaze +
                    "\" | \"toggleButtonState\":\"" + toggleButtonState + "\"}";

                string gazePipe =
                    "{\"type\":\"gaze\" | \"fixation\":\"" + Vec3Pipe(fixation) +
                    "\" | \"origin\":\"" + Vec3Pipe(gazeOrigin) +
                    "\" | \"dir\":\"" + Vec3Pipe(gazeDir) + "\"}";

                string qrPipe =
                    "{\"type\":\"qr\" | \"distance\":\"" + qrDistance.ToString("F4") +
                    "\" | \"direction\":\"" + Vec3Pipe(qrDirection) +
                    "\" | \"position\":\"" + Vec3Pipe(qrPosition) +
                    "\" | \"rotation\":\"" + QuatPipe(qrRotation) + "\"}";

                string facesPipe = (facialDetection != null)
                    ? facialDetection.GetLastDetectionsPipeString()
                    : "{\"type\":\"faces\" | \"count\":\"0\" | \"faces\":\"\"}";

                // "Pipe JSON" segments are concatenated with '|' so the entire payload remains a single CSV column (no commas).
                string logData = statusPipe + "|" + gazePipe + "|" + qrPipe + "|" + facesPipe;

                evaluatAR.writeToLogsFile(logData);
            }
        }

        UpdateFaceCubeRotation();
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

    // Handles the event for the Bumper.
    private void HandleOnBumper(InputAction.CallbackContext obj)
    {
        if (DebugCubeMode)
        {
            // toggle DebugCube and FPS text on or off
            DebugQuadRenderer.enabled = !DebugQuadRenderer.enabled;
            // keep track of it debug quad is on or off
            DebugCubeStatus = DebugQuadRenderer.enabled;

            //FPSTextBox.gameObject.SetActive(DebugQuadRenderer.enabled);
        }
    }

    // Handles updation of DebugCube's position with the MainCamera
    private void UpdateDebugCubePosition()
    {
        DebugQuadRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + (0.25f * Vector3.right);
        DebugQuadRenderer.transform.rotation = MainCamera.transform.rotation;
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
            // Flips the frame vertically so it does not appear upside down.
            MLCamera.FlipFrameVertically(ref output);

            UpdateRGBTexture(ref _videoTextureRgb, output.Planes[0], resultExtras);
            tempTexture = _videoTextureRgb;
            tempTexture.Apply();
            _hasNewVideoFrame = true;
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
                //DebugQuadRenderer.material.mainTexture = bboxTexture;
                 DebugQuadRenderer.material.mainTexture = videoTextureRGB;
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
            videoTextureRGB.Apply();

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