using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

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
        // Debug.Log("Inside QR dectection OnTrackerResultsFound");

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
                // marker.transform.position = data.Pose.position;
                // marker.transform.rotation = data.Pose.rotation;

                float distance = Vector3.Distance(MainCamera.transform.position, data.Pose.position);
                // Debug.Log("Distance from QR code with id " + id + " : " + distance);

                Vector3 direction = data.Pose.position - MainCamera.transform.position;
                // Debug.Log("Direction from QR code with id " + id + " : " + direction);

                latestDirection = direction;
                latestDistance = distance;

                latestPosition = data.Pose.position;
                latestRotation = data.Pose.rotation;

                // float angle = Quaternion.Angle(MainCamera.transform.rotation, data.Pose.rotation);
                // Debug.Log("Angle between QR code with id " + id + " and camera : " + angle);
            }
            else
            {
                //Create a primitive cube
                // GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                GameObject marker = new GameObject(id);
                //Render the cube with the default URP shader
                // marker.AddComponent<Renderer>();
                // marker.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                // marker.transform.position = data.Pose.position;
                // marker.transform.rotation = data.Pose.rotation;
                // marker.transform.localScale = new Vector3(markerSize, markerSize, markerSize);

                float distance = Vector3.Distance(MainCamera.transform.position, data.Pose.position);
                // Debug.Log("Distance from QR code with id " + id + " : " + distance);

                Vector3 direction = data.Pose.position - MainCamera.transform.position;
                // Debug.Log("Direction from QR code with id " + id + " : " + direction);

                latestDirection  = direction;
                latestDistance = distance;

                latestPosition = data.Pose.position;
                latestRotation = data.Pose.rotation;

                // float angle = Quaternion.Angle(MainCamera.transform.rotation, data.Pose.rotation);
                // Debug.Log("Angle between QR code with id " + id + " and camera : " + angle);

                _markers.Add(id, marker);
            }
        }
    }

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