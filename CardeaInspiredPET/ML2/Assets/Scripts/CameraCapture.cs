using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.MagicLeap;

public class CameraCapture : MonoBehaviour
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
    private MLCamera.CaptureConfig _captureConfig;

    [SerializeField]
    private GameObject DebugQuad;
    private Renderer DebugQuadRenderer;

    // turns off all logging 
    public bool LoggingMode = true;
    // false: turns off debug cube position update, debug cube renderer, texture updates 
    public bool DebugCubeMode = true;
    private bool DebugCubeStatus = true; // true: debug cube is enabled so update it's position and FPS display

    // camera intrinsics and extrinsics for a given frame
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
    private GameObject anotherPET;
    private AnotherPET anotherPETScript;


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

    void Start()
    {
        // initialize InputActionAsset, ControllerActions using ML Input
        _magicLeapInputs = new MagicLeapInputs();
        _magicLeapInputs.Enable();
        _controllerActions = new MagicLeapInputs.ControllerActions(_magicLeapInputs);

        // Subscribe to controller events
        _controllerActions.Bumper.performed += HandleOnBumper;

        DebugQuadRenderer = DebugQuad.GetComponent<Renderer>();
        anotherPETScript = anotherPET.GetComponent<AnotherPET>();

        if (!DebugCubeMode)
        {
            DebugQuadRenderer.enabled = false;
            DebugCubeStatus = false;
        }
    }

    void Update()
    {
        MainCameraTransformPosition = MainCamera.transform.position;

        if (DebugCubeMode && DebugCubeStatus)
        {
            // always keep DebugCube next to player
            UpdateDebugCubePosition();
        }
    }

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
            // toggle DebugCube on or off
            DebugQuadRenderer.enabled = !DebugQuadRenderer.enabled;
            // keep track of it debug quad is on or off
            DebugCubeStatus = DebugQuadRenderer.enabled;
        }
    }

    // Handles updation of DebugCube's position with the MainCamera
    private void UpdateDebugCubePosition()
    {
        DebugQuadRenderer.transform.position = MainCameraTransformPosition + MainCamera.transform.forward + (0.25f * Vector3.right);
        DebugQuadRenderer.transform.rotation = MainCamera.transform.rotation;
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
            // Flips the frame vertically so it does not appear upside down.
            MLCamera.FlipFrameVertically(ref output);

            UpdateRGBTexture(ref _videoTextureRgb, output.Planes[0], resultExtras);
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

            //if (DebugCubeMode)
            //{
            //    DebugQuadRenderer.material.mainTexture = videoTextureRGB;
            //}
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
            //videoTextureRGB.Apply(); // TODO: do i need to do this? or can this be removed to save computation costs?
        }

        // ADD HOOK FOR TO GET CAMERA FRAMES
        anotherPETScript.setCurrFrame(videoTextureRGB, videoTextureRGB.width, videoTextureRGB.height);
    }
}