//using System;
//using System.IO;
//using System.Linq;
//using System.Collections;
//using System.Diagnostics;
//using Debug = UnityEngine.Debug;
//using System.Collections.Generic;

//using UnityEngine;
//using UnityEngine.UI;

//using OpenCVForUnity.UnityUtils;
//using OpenCVForUnity.CoreModule;
//using OpenCVForUnity.ImgprocModule;
//using OpenCVForUnity.ObjdetectModule;

//public class BumperOverride : MonoBehaviour
//{
//    // public variables //

//    // private variables //
//    private string dirPath;

//    private Camera MainCamera;
//    private Vector3 MainCameraTransformPosition;

//    [SerializeField]
//    private Renderer cameraRenderer;

//    [SerializeField]
//    private GameObject CameraCaptureObject;
//    private CameraCapture CameraCaptureScript;
//    private WebCamTexture CameraFrame;
//    private int minCaptureWidth = 0;
//    private int minCaptureHeight = 0;
//    private int captureWidth = 1280;
//    private int captureHeight = 960;
//    private Texture2D CameraFrameTexture2D;

//    private LayerMask mask;

//    private Size captureSize;
//    private Mat latestReturnedMatFromModel;

//    [SerializeField]
//    private GameObject DebugQuad;
//    private Renderer DebugQuadRenderer;
    

//    [SerializeField]
//    private GameObject FacialDetectionObject;
//    private FacialDetection FacialDetctionScript;
//    [SerializeField]
//    private GameObject FaceCubePrefab;
//    private FaceCubeScript FaceCubePrefabScript;

//    // [SerializeField]
//    // private GameObject Logger;
//    // private CSVLogger CSVLoggerScript;

//    // [SerializeField]
//    // private GameObject ToggleButton;
//    // private Renderer ToggleButtonRenderer;
//    // [SerializeField]
//    // private Vector3 toggleButtonOffset = new Vector3(-1.5f, -3.5f, 1f);

//    [SerializeField] // turns off all logging 
//    private bool LoggingMode = true;
//    [SerializeField] // false: turns off debug cube position update, debug cube renderer, texture updates 
//    private bool DebugCubeMode = true;
//    private bool DebugCubeStatus = true; // true: debug cube is enabled so update it's position and FPS display 

//    [HideInInspector]
//    public int FrameCounter = 0;

//    private DateTime lastCallToDrawPred = DateTime.Now;

//    [SerializeField]
//    private int InferenceFrameStep = 1;
//    [SerializeField]
//    private int LoggingFrameStep = 1;

//    private GameObject[] faceCubesInScene;
//    private int faceCubeCounter = 1;

//    Vector3 direction = new Vector3(0, 0, 0);

//    private Stopwatch timer = new Stopwatch();
//    private bool toggleButtonStatus = false;

//    private Matrix4x4 currCameraTransform = Matrix4x4.identity;
//    private uint currIcpWidth = 0;
//    private uint currIcpHeight = 0;
//    private float currIcpFOV = 0;
//    private Vector2 currIcpFocalLength = new Vector2(0, 0);
//    private Vector2 currIcpPrincipalPoint = new Vector2(0, 0);
//    private float currIcpDistortion0 = 0;
//    private float currIcpDistortion1 = 0;
//    private float currIcpDistortion2 = 0;
//    private float currIcpDistortion3 = 0;
//    private float currIcpDistortion4 = 0;

//    [SerializeField]
//    private bool SaveReferenceImage = false;
//    private int savedCounter = 0;
//    private Texture2D tempTexture;

//    [SerializeField]
//    private bool predictPositionMode = false;
//    [SerializeField]
//    private bool closestDepthMode = false;
//    [SerializeField]
//    private bool kalmanFilterMode = false;

//    [SerializeField]
//    private float depthWeight = 2.0f;
//    [SerializeField]
//    private float twoDDistanceWeight = 0.2f;


//    // functions //

//    // initialize vairables before application starts
//    void Awake()
//    {
//        // Enabled or disbaled debug logs for whole application
//        Debug.unityLogger.logEnabled = LoggingMode;

//        dirPath = Application.persistentDataPath + "/SavedImages/";

//        MainCamera = Camera.main;
//    }

//    // Start is called before the first frame update
//    void Start()
//    {
//        CameraFrameTexture2D = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);

//        // get DebugCube's material to switch it on or off
//        DebugQuadRenderer = DebugQuad.GetComponent<Renderer>();

//        if (!DebugCubeMode)
//        {
//            DebugQuadRenderer.enabled = false;
//            DebugCubeStatus = false;
//        }

//        captureSize = new Size(captureWidth, captureHeight);

//        CameraCaptureScript = CameraCaptureObject.GetComponent<CameraCapture>();

//        FacialDetctionScript = FacialDetectionObject.GetComponent<FacialDetection>();

//        //  face cube script
//        FaceCubePrefabScript = FaceCubePrefab.GetComponent<FaceCubeScript>();

//        // we need collision with only the Face Cubes layer so ray cast only returns face cubes
//        mask = LayerMask.GetMask("Face Cubes");

//        // setting texture to display on the quad
//        cameraRenderer.material.mainTexture = CameraFrameTexture2D;

//    }

//    // Update is called once per frame
//    void Update()
//    {
//        faceCubesInScene = GameObject.FindGameObjectsWithTag("FaceCubePrefab");

//        MainCameraTransformPosition = MainCamera.transform.position;

//        CameraFrame = null;// CameraCaptureScript.cameraFrame;
//        if (CameraFrame != null)
//        {
//            CameraFrameTexture2D.SetPixels(CameraFrame.GetPixels());
//            CameraFrameTexture2D.Apply();

//            FrameCounter++;

//            // Tuple<List<Vector2>, List<drawPredInfo>>
//            var pixelFaceCentersAndDrawPreds = Predict(CameraFrameTexture2D);//, resultExtras);

//            // render detected faces' bboxes as cubes in world coordinates
//            faceCubeInWorldVisualization(pixelFaceCentersAndDrawPreds.Item1, pixelFaceCentersAndDrawPreds.Item2);

//            // keep checking which face cube (if any) is eye gaze interacting with
//            ThresholdingLogicForFaceCube();

//            // get the face cubes of bystanders (isSubject == false) and draw bbox for them on debug cube texture
//            DrawBystanderBboxes();
//        }

//        if (OVRInput.Get(OVRInput.RawButton.RIndexTrigger)) HandleOnBumper();

//        UpdateFaceCubeRotation();

//        // CameraCaptureScript.cameraFrame.origin;
//        direction = CameraCaptureScript.direction;
//        toggleButtonStatus = CameraCaptureScript.toggleButtonState;


//        if (FrameCounter % LoggingFrameStep == 0)
//        {
//            // CSV Logger
//            CameraCaptureScript.FPSCalculator(faceCubesInScene);
//        }

//        // save reference image too
//        if (SaveReferenceImage)
//        {
//            bool positionedStatus = CameraCaptureScript.initiallyPositioned;

//            if (positionedStatus && savedCounter == 0)
//            {
//                string referenceImageFileName = DateTime.Now.ToString("HH-mm-ss.ffff");
//                string referenceImagePath = dirPath + referenceImageFileName + ".png";

//                File.WriteAllBytes(referenceImagePath, tempTexture.EncodeToPNG());

//                savedCounter++;
//            }
//        }

//        // drae PredPosition for all Facecubes
//        // drawPredPos();
//    }

//    // Handles updation of FaceCubes' position with the MainCamera
//    private void UpdateFaceCubeRotation()
//    {
//        for (int i = 0; i < faceCubesInScene.Length; i++)
//        {
//            faceCubesInScene[i].transform.LookAt(MainCamera.transform);
//        }
//    }

//    private void drawPredPos()
//    {
//        for (int i = 0; i < faceCubesInScene.Length; i++)
//        {
//            var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();
//        }
//    }

//    // Handles the disposing all of the input events.
//    void OnDestroy()
//    {
//    }

//    // Handles the disposing all of the input events.
//    void OnDisable()
//    {
//    }

//    // Handles the event for the Bumper.
//    private void HandleOnBumper()
//    {
//        if (DebugCubeMode)
//        {
//            // toggle DebugCube and FPS text on or off
//            DebugQuadRenderer.enabled = !DebugQuadRenderer.enabled;
//            // keep track of it debug quad is on or off
//            DebugCubeStatus = DebugQuadRenderer.enabled;
//        }
//    }

//    private Tuple<List<Vector2>, List<drawPredInfo>> Predict(Texture2D videoTextureRGB)//, MLCamera.ResultExtras resultExtras)
//    {
//        // Tuple<Mat, List<Vector2>, List<drawPredInfo>>
//        if (FrameCounter % InferenceFrameStep == 0)
//        {
//            var detectionFrameCentersAndDrawPredInfo = FacialDetctionScript.RunDetection(videoTextureRGB);

//            latestReturnedMatFromModel = detectionFrameCentersAndDrawPredInfo.Item1;

//            return new Tuple<List<Vector2>, List<drawPredInfo>>(detectionFrameCentersAndDrawPredInfo.Item2, detectionFrameCentersAndDrawPredInfo.Item3);
//        }
//        else
//        {
//            latestReturnedMatFromModel = FacialDetctionScript.MatFromTexture(videoTextureRGB);

//            // update 2D coords of each rendered face cube using its 3D position for every non-inference frame
//            worldToPixelConversion();

//            List<Vector2> listOfVector2 = new List<Vector2>();
//            List<drawPredInfo> drawPredInfoList = new List<drawPredInfo>();
//            return new Tuple<List<Vector2>, List<drawPredInfo>>(listOfVector2, drawPredInfoList);
//        }
//    }

//    private void faceCubeInWorldVisualization(List<Vector2> pixelFaceCentersList, List<drawPredInfo> drawPredInfoList)
//    {
//        List<string> assignedFaceCubeNames = new List<string>();

//        for (int i = 0; i < pixelFaceCentersList.Count; i++)
//        {
//            var resultPosition = CameraCaptureScript.cameraToWorldConversion((int)pixelFaceCentersList[i].x, (int)pixelFaceCentersList[i].y);

//            var overlapBoxes = Physics.OverlapBox(resultPosition, FaceCubePrefab.transform.localScale / 2, Quaternion.identity);//, mask);

//            if (overlapBoxes.Length > 0 && overlapBoxes[0].gameObject.tag == "FaceCubePrefab")
//            {
//                overlapBoxes[0].gameObject.transform.position = resultPosition;

//                var faceCubeScript = overlapBoxes[0].gameObject.GetComponent<FaceCubeScript>();
//                faceCubeScript.setStaleCounter();

//                // update it's drawPred details as well
//                faceCubeScript.cid = drawPredInfoList[i].classId;
//                faceCubeScript.c = drawPredInfoList[i].conf;
//                faceCubeScript.l = drawPredInfoList[i].left;
//                faceCubeScript.t = drawPredInfoList[i].top;
//                faceCubeScript.r = drawPredInfoList[i].right;
//                faceCubeScript.b = drawPredInfoList[i].bottom;

//                faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
//                faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

//                faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
//                faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
//            }
//            else
//            {
//                var newObject = Instantiate(FaceCubePrefab, resultPosition, Quaternion.identity);
//                newObject.name = "FaceCube-" + faceCubeCounter.ToString();
//                faceCubeCounter++;

//                var faceCubeScript = newObject.GetComponent<FaceCubeScript>();

//                // update it's drawPred details as well
//                faceCubeScript.cid = drawPredInfoList[i].classId;
//                faceCubeScript.c = drawPredInfoList[i].conf;
//                faceCubeScript.l = drawPredInfoList[i].left;
//                faceCubeScript.t = drawPredInfoList[i].top;
//                faceCubeScript.r = drawPredInfoList[i].right;
//                faceCubeScript.b = drawPredInfoList[i].bottom;

//                faceCubeScript.detection_width = drawPredInfoList[i].right - drawPredInfoList[i].left;
//                faceCubeScript.detection_height = drawPredInfoList[i].top - drawPredInfoList[i].bottom;

//                faceCubeScript.xCenter = (drawPredInfoList[i].right + drawPredInfoList[i].left) / 2;
//                faceCubeScript.yCenter = (drawPredInfoList[i].top + drawPredInfoList[i].bottom) / 2;
//            }
//        }
//    }

//    private void ThresholdingLogicForFaceCube()
//    {
//        // check the face cube that eye gaze is colliding with and pick the first one
//        if (Physics.Raycast(MainCameraTransformPosition, direction, out RaycastHit hit, 100, mask))
//        {
//            GameObject targetFaceCube = hit.transform.gameObject;
//            var targetFaceCubeScript = hit.transform.gameObject.GetComponent<FaceCubeScript>();

//            if (targetFaceCubeScript.getIslooking() == false)
//            {
//                targetFaceCubeScript.setIslooking(true);
//                targetFaceCubeScript.EyeContactStarted();
//            }
//            else
//            {
//                targetFaceCubeScript.setIslooking(true);
//                targetFaceCubeScript.EyeContactMaintained();
//            }

//            // isLooking for all other facecube's should be false
//            for (int i = 0; i < faceCubesInScene.Length; i++)
//            {
//                if (faceCubesInScene[i].name != targetFaceCube.name)
//                {
//                    var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

//                    if (faceCubeScript.getIslooking() == true)
//                    {
//                        faceCubeScript.EyeContactLost();
//                        faceCubeScript.setIslooking(false);
//                    }
//                }
//            }

//            // check if talking while looking at this face cube as well
//            // bool currentlyIsTalking = AudioCaptureObjectScript.getIsTalking();
//            // targetFaceCubeScript.setIsTalking(currentlyIsTalking);
//        }
//        else
//        {
//            // isLooking for all facecube's should be false
//            for (int i = 0; i < faceCubesInScene.Length; i++)
//            {
//                var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();
//                if (faceCubeScript.getIslooking() == true)
//                {
//                    faceCubeScript.EyeContactLost();
//                    faceCubeScript.setIslooking(false);
//                }
//            }
//        }
//    }

//    private void DrawBystanderBboxes()
//    {
//        //  check if there are any detections or not, if not then apply the texture as it is
//        if (faceCubesInScene.Length == 0)
//        {
//            if (DebugCubeMode && DebugCubeStatus)
//            {
//                // Utils.matToTexture2D(latestReturnedMatFromModel, bboxTexture);
//                // bboxTexture.Apply();

//                Utils.matToTexture2D(latestReturnedMatFromModel, CameraFrameTexture2D);
//                CameraFrameTexture2D.Apply();
//            }
//        }
//        else
//        {
//            Mat matWithDetections = latestReturnedMatFromModel;

//            for (int i = 0; i < faceCubesInScene.Length; i++)
//            {
//                // get facecube's script
//                var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

//                // check if face cube is of bystander - ignore if it is a subject
//                if (faceCubeScript.getIsSubject() == true)
//                {
//                    continue;
//                }

//                // for bystanders get the 2D coorindates and call drawPreds to make bboxes on texture
//                matWithDetections = FacialDetctionScript.drawPred(faceCubeScript.cid, faceCubeScript.c, faceCubeScript.l, faceCubeScript.t, faceCubeScript.r, faceCubeScript.b, latestReturnedMatFromModel);
//            }

//            if (DebugCubeMode && DebugCubeStatus)
//            {
//                // Utils.matToTexture2D(matWithDetections, bboxTexture);
//                // bboxTexture.Apply();

//                Utils.matToTexture2D(matWithDetections, CameraFrameTexture2D);
//                CameraFrameTexture2D.Apply();
//            }

//        }
//    }

//    private void worldToPixelConversion()
//    {
//        for (int i = 0; i < faceCubesInScene.Length; i++)
//        {
//            Vector3 currWorldCoords = faceCubesInScene[i].transform.position;

//            /*
//            if (currCameraTransform != Matrix4x4.identity && currIcpWidth != 0 && currIcpHeight != 0 && currIcpFOV != 0 && currIcpFocalLength != Vector2.zero && currIcpPrincipalPoint != Vector2.zero)
//            {
//                // var pixelPosition = CameraUtilities.ConvertWorldPointToScreen(currIcp, currCameraTransform, currWorldCoords);
//                var pixelPosition = CameraUtilities.ConvertWorldPointToScreen(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, currWorldCoords);

//                // store the latest 2D coords to be used to draw preds on texture
//                var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

//                double left = pixelPosition.x - (faceCubeScript.detection_width / 2.0);
//                double top = pixelPosition.y + (faceCubeScript.detection_height / 2.0);
//                double right = pixelPosition.x + (faceCubeScript.detection_width / 2.0);
//                double bottom = pixelPosition.y - (faceCubeScript.detection_height / 2.0);

//                // TO DO: Clean this code up by using Math.Clamp (or something similar)
//                if (left < (double)minCaptureWidth)
//                {
//                    faceCubeScript.l = (double)minCaptureWidth;
//                }
//                else
//                {
//                    faceCubeScript.l = left;
//                }

//                if (top > (double)captureHeight)
//                {
//                    faceCubeScript.t = (double)captureHeight;
//                }
//                else
//                {
//                    faceCubeScript.t = top;
//                }

//                if (right > (double)captureWidth)
//                {
//                    faceCubeScript.r = (double)captureWidth;
//                }
//                else
//                {
//                    faceCubeScript.r = right;
//                }

//                if (bottom < (double)minCaptureHeight)
//                {
//                    faceCubeScript.b = (double)minCaptureHeight;
//                }
//                else
//                {
//                    faceCubeScript.b = bottom;
//                }
//            }
//            */

//            // var pixelPosition = CameraUtilities.ConvertWorldPointToScreen(currIcp, currCameraTransform, currWorldCoords);
//            // var pixelPosition = CameraUtilities.ConvertWorldPointToScreen(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, currCameraTransform, currWorldCoords);
//            var pixelPosition = CameraCaptureScript.worldToCameraConversion(currWorldCoords);


//            // store the latest 2D coords to be used to draw preds on texture
//            var faceCubeScript = faceCubesInScene[i].GetComponent<FaceCubeScript>();

//            double left = pixelPosition.x - (faceCubeScript.detection_width / 2.0);
//            double top = pixelPosition.y + (faceCubeScript.detection_height / 2.0);
//            double right = pixelPosition.x + (faceCubeScript.detection_width / 2.0);
//            double bottom = pixelPosition.y - (faceCubeScript.detection_height / 2.0);

//            // TO DO: Clean this code up by using Math.Clamp (or something similar)
//            if (left < (double)minCaptureWidth)
//            {
//                faceCubeScript.l = (double)minCaptureWidth;
//            }
//            else
//            {
//                faceCubeScript.l = left;
//            }

//            if (top > (double)captureHeight)
//            {
//                faceCubeScript.t = (double)captureHeight;
//            }
//            else
//            {
//                faceCubeScript.t = top;
//            }

//            if (right > (double)captureWidth)
//            {
//                faceCubeScript.r = (double)captureWidth;
//            }
//            else
//            {
//                faceCubeScript.r = right;
//            }

//            if (bottom < (double)minCaptureHeight)
//            {
//                faceCubeScript.b = (double)minCaptureHeight;
//            }
//            else
//            {
//                faceCubeScript.b = bottom;
//            }
//        }
//    }
//}