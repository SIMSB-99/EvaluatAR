using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;

using ZXing;

using Meta.XR;
using Meta.XR.Samples;
using PassthroughCameraSamples;


public class ArUcoDetector : MonoBehaviour
{
    private Camera MainCamera;

    [SerializeField]
    private GameObject anotherPET;
    private AnotherPET anotherPETScript;

    [SerializeField]
    private GameObject cameraCapture;
    private CameraCapture cameraCaptureScript;
    private PassthroughCameraEye CameraEye;
    private EnvironmentRaycastManager environmentRaycastManager;

    private Texture2D cameraFrame = null;

    private float latestDistance;
    private Vector3 latestDirection;
    private Vector3 latestPosition;
    private Quaternion latestRotation;

    private bool isDisabled = false;

    //private PassthroughCameraEye CameraEye => webCamTextureManager.Eye;


    void Awake()
    {
        environmentRaycastManager = GetComponent<EnvironmentRaycastManager>();

        MainCamera = Camera.main;
    }

    void Start()
    {
        anotherPETScript = anotherPET.GetComponent<AnotherPET>();
        cameraCaptureScript = cameraCapture.GetComponent<CameraCapture>();

        cameraFrame = new Texture2D(1280, 960, TextureFormat.RGBA32, false);

        CameraEye = cameraCaptureScript.getCameraEye();
        Debug.Log("CameraEye: " + CameraEye);
    }

    
    void Update()
    {
        if (!isDisabled)
        {
            cameraFrame = anotherPETScript.getCurrCameraFrame();

            if (cameraFrame != null)
            {
                detectQRCode();
            }
        } 
    }

    private void detectQRCode() 
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

            Pose pose = QRCodeConvertScreenPointToWorldPoint(qrCodeCenter);

            latestPosition = pose.position;
            latestRotation = pose.rotation;
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
        Debug.Log("screenpoint: " + screenPoint.ToString());

        var ray = PassthroughCameraUtils.ScreenPointToRayInWorld(CameraEye, screenPoint);

        Debug.Log("ray: " + ray.ToString());

        // By default, set the hit point at a fixed length away.
        Vector3 hitPoint = ray.GetPoint(3);

        if (environmentRaycastManager.Raycast(ray, out var hitInfo, 100))
        {
            Debug.Log("hit something: " + hitInfo.ToString());
            // Debug.LogError("BystandAR -- QRCode screen to world ray hit something");

            Pose pose = new Pose(hitInfo.point, Quaternion.FromToRotation(Vector3.up, hitInfo.normal));
            return pose;
        }
        else
        {
            Debug.Log("Did not hit anything");
            return Pose.identity;
        }
    }

    public float getCurrQrDistance()
    {
        return latestDistance;
    }

    public Vector3 getCurrQrDirection()
    {
        return latestDirection;
    }

    public Vector3 getCurrQrPosition()
    {
        return latestPosition;
    }

    public Quaternion getCurrQrRotation()
    {
        return latestRotation;
    }

    public void OnDisable()
    {
        isDisabled = true;
    }
}
