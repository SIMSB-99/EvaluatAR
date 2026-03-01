using UnityEngine;
using UnityEngine.XR.MagicLeap;

using System.Text;
using System.Collections.Generic;


public class ArUcoDetector : MonoBehaviour
{
    private Camera MainCamera;

    private float latestDistance;
    private Vector3 latestDirection;
    private Vector3 latestPosition;
    private Quaternion latestRotation;

    public float QrCodeMarkerSize = 0.1f;
    public float ArucoMarkerSize = 0.1f;
    public MLMarkerTracker.MarkerType Type = MLMarkerTracker.MarkerType.QR;
    public MLMarkerTracker.ArucoDictionaryName ArucoDict = MLMarkerTracker.ArucoDictionaryName.DICT_5X5_100;
    public MLMarkerTracker.Profile Profile = MLMarkerTracker.Profile.Default;

    private Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>();
    private ASCIIEncoding _asciiEncoder = new System.Text.ASCIIEncoding();


    private void Awake()
    {
        MainCamera = Camera.main;
    }

    private void OnEnable()
    {
        MLMarkerTracker.OnMLMarkerTrackerResultsFound += OnTrackerResultsFound;
    }

    private void Start()
    {
        MLMarkerTracker.TrackerSettings trackerSettings = MLMarkerTracker.TrackerSettings.Create(true, Type, QrCodeMarkerSize, ArucoDict, ArucoMarkerSize, Profile);
        _ = MLMarkerTracker.SetSettingsAsync(trackerSettings);
    }

    public void OnDisable()
    {
        // Debug.Log("Inside QR dectection OnDisable");

        MLMarkerTracker.OnMLMarkerTrackerResultsFound -= OnTrackerResultsFound;
        _ = MLMarkerTracker.StopScanningAsync();
    }

    private void OnTrackerResultsFound(MLMarkerTracker.MarkerData data)
    {
        string id = "";
        float markerSize = .01f;

        switch (data.Type)
        {
            case MLMarkerTracker.MarkerType.Aruco_April:
                id = data.ArucoData.Id.ToString();
                markerSize = ArucoMarkerSize;
                break;

            case MLMarkerTracker.MarkerType.QR:
                id = _asciiEncoder.GetString(data.BinaryData.Data, 0, data.BinaryData.Data.Length);
                markerSize = QrCodeMarkerSize;
                break;
            case MLMarkerTracker.MarkerType.EAN_13:
            case MLMarkerTracker.MarkerType.UPC_A:
                id = _asciiEncoder.GetString(data.BinaryData.Data, 0, data.BinaryData.Data.Length);
                Debug.Log("No pose is given for marker type " + data.Type + " value is " + data.BinaryData.Data);
                break;
        }

        if (!string.IsNullOrEmpty(id))
        {
            if (_markers.ContainsKey(id))
            {
                GameObject marker = _markers[id];

                float distance = Vector3.Distance(MainCamera.transform.position, data.Pose.position);
                Vector3 direction = data.Pose.position - MainCamera.transform.position;

                latestDirection = direction;
                latestDistance = distance;
                latestPosition = data.Pose.position;
                latestRotation = data.Pose.rotation;
            }
            else
            {
                //Create a primitive cube
                GameObject marker = new GameObject(id);

                float distance = Vector3.Distance(MainCamera.transform.position, data.Pose.position);;
                Vector3 direction = data.Pose.position - MainCamera.transform.position;

                latestDirection  = direction;
                latestDistance = distance;
                latestPosition = data.Pose.position;
                latestRotation = data.Pose.rotation;

                _markers.Add(id, marker);
            }
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


    // TODO: these are temp; delete after testing
    public float getLatestDistance()
    {
        return latestDistance;
    }

    public Vector3 getLatestDirection()
    {
        return latestDirection;
    }

    public Vector3 getLatestPosition()
    {
        return latestPosition;
    }

    public Quaternion getLatestRotation()
    {
        return latestRotation;
    }
}