using OpenCVForUnityExample.DnnModel;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UtilsModule;
using OpenCVForUnity.UnityUtils.Helper;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

using OpenCVForUnity.UnityUtils;


public class HandPoseDetection : MonoBehaviour
{
    /// <summary>
    /// The bgr mat.
    /// </summary>
    Mat bgrMat;
    Mat rgbaMat;
    private Texture2D handPoseViz = null;
    private Size size;

    /// <summary>
    /// The palm detector.
    /// </summary>
    MediaPipePalmDetector palmDetector;

    /// <summary>
    /// The handpose estimator.
    /// </summary>
    MediaPipeHandPoseEstimator handPoseEstimator;

    /// <summary>
    /// PALM_DETECTION_MODEL_FILENAME
    /// </summary>
    public string PALM_DETECTION_MODEL_FILENAME = "OpenCVForUnity/dnn/palm_detection_mediapipe_2023feb.onnx";
    public float palm_conf_threshold = 0.6f;

    /// <summary>
    /// The palm detection model filepath.
    /// </summary>
    string palm_detection_model_filepath;

    /// <summary>
    /// HANDPOSE_ESTIMATION_MODEL_FILENAME
    /// </summary>
    public string HANDPOSE_ESTIMATION_MODEL_FILENAME = "OpenCVForUnity/dnn/handpose_estimation_mediapipe_2023feb.onnx";
    public float handpose_conf_threshold = 0.6f;

    /// <summary>
    /// The handpose estimation model filepath.
    /// </summary>
    string handpose_estimation_model_filepath;

    public enum Gesture
    {
        Unknown,
        OpenPalm,
        Fist,
        Point,
        TwoFingers,
        ThumbsUp,
        Pinch
    }


    // Use this for initialization
    void Awake()
    {
        //if true, The error log of the Native side OpenCV will be displayed on the Unity Editor Console.
        Utils.setDebugMode(true);

        palm_detection_model_filepath = Utils.getFilePath(PALM_DETECTION_MODEL_FILENAME);
        handpose_estimation_model_filepath = Utils.getFilePath(HANDPOSE_ESTIMATION_MODEL_FILENAME);

        if (string.IsNullOrEmpty(palm_detection_model_filepath))
        {
            Debug.LogError(PALM_DETECTION_MODEL_FILENAME + " is not loaded. Please read “StreamingAssets/OpenCVForUnity/dnn/setup_dnn_module.pdf” to make the necessary setup.");
        }
        else
        {
            try {
                palmDetector = new MediaPipePalmDetector(palm_detection_model_filepath, 0.3f, palm_conf_threshold);
                Debug.Log("MediaPipePalmDetector created OK");
            } catch (Exception e)
            {
                Debug.LogError("palmDetector model init failed: " + e);
            }            
        }

        if (string.IsNullOrEmpty(handpose_estimation_model_filepath))
        {
            Debug.LogError(HANDPOSE_ESTIMATION_MODEL_FILENAME + " is not loaded. Please read “StreamingAssets/OpenCVForUnity/dnn/setup_dnn_module.pdf” to make the necessary setup.");
        }
        else
        {
            try
            {
                handPoseEstimator = new MediaPipeHandPoseEstimator(handpose_estimation_model_filepath, handpose_conf_threshold);
                Debug.Log("MediaPipeHandPoseEstimator created OK");
            }
            catch (Exception e)
            {
                Debug.LogError("handPoseEstimator model init failed: " + e);
            }
        }

        size = new Size(1280, 720);
        handPoseViz = new Texture2D(1280, 720, TextureFormat.RGBA32, false);

        rgbaMat = new Mat(size, CvType.CV_8UC3);
        bgrMat = new Mat(size, CvType.CV_8UC3);

        Debug.Log("palm_detection_model_filepath: " + palm_detection_model_filepath);
        Debug.Log("handpose_estimation_model_filepath: " + handpose_estimation_model_filepath);
    }

    //// Use this for initialization
    ////void Run()
    //public Texture2D RunDetection(Texture2D frame)
    //{
    //    //if true, The error log of the Native side OpenCV will be displayed on the Unity Editor Console.
    //    //Utils.setDebugMode(true);

    //    rgbaMat = new Mat(size, CvType.CV_8UC3);
    //    bgrMat = new Mat(size, CvType.CV_8UC3);

    //    rgbaMat = MatFromTexture(frame);

    //    if (palmDetector == null || handPoseEstimator == null)
    //    {
    //        Debug.Log("palmDetector == null || handPoseEstimator == null; model(s) not loaded properly");

    //    }
    //    else
    //    {
    //        Imgproc.cvtColor(rgbaMat, bgrMat, Imgproc.COLOR_RGBA2BGR);

    //        //TickMeter tm = new TickMeter();
    //        //tm.start();

    //        // detects hands and their positions
    //        Mat palms = palmDetector.infer(bgrMat);



    //        //  at this point we have a Mat 

    //        //tm.stop();
    //        //Debug.Log("MediaPipePalmDetector Inference time (preprocess + infer + postprocess), ms: " + tm.getTimeMilli());

    //        List<Mat> hands = new List<Mat>();

    //        // Estimate the pose of each hand
    //        for (int i = 0; i < palms.rows(); ++i)
    //        {
    //            //tm.reset();
    //            //tm.start();

    //            // Handpose estimator inference
    //            Mat handpose = handPoseEstimator.infer(bgrMat, palms.row(i));

    //            //tm.stop();
    //            //Debug.Log("MediaPipeHandPoseEstimator Inference time (preprocess + infer + postprocess), ms: " + tm.getTimeMilli());

    //            if (!handpose.empty())
    //                hands.Add(handpose);
    //        }

    //        Imgproc.cvtColor(bgrMat, rgbaMat, Imgproc.COLOR_BGR2RGBA);

    //        //palmDetector.visualize(rgbaMat, palms, false, true);

    //        foreach (var hand in hands)
    //            handPoseEstimator.visualize(rgbaMat, hand, false, true);
    //    }

    //    Utils.matToTexture2D(rgbaMat, handPoseViz);
    //    //handPoseViz.Apply();

    //    return handPoseViz;
    //}

    //public Texture2D RunPalmDetector(Texture2D frame)
    public Tuple<List<drawPredInfo>, Mat> RunPalmDetector(Mat rgbaInput)
    {
        if (rgbaInput == null || rgbaInput.empty())
        {
            Debug.Log("Input Mat was null");
            return new Tuple<List<drawPredInfo>, Mat>(new List<drawPredInfo>(), new Mat());
        }

        // Keep internal rgbaMat in sync
        rgbaInput.copyTo(rgbaMat);

        Imgproc.cvtColor(rgbaMat, bgrMat, Imgproc.COLOR_RGBA2BGR);

        // detects hands and their positions
        Mat palms = palmDetector.infer(bgrMat);


        //extract top left bottom right x and y coords for each detected palm/hand
        List<drawPredInfo> drawPredInfoForPalmsList = new List<drawPredInfo>();   
        for (int i = 0; i < palms.rows(); i++)
        {
            float[] bb = new float[4];
            palms.get(i, 0, bb);

            float topleft_x = bb[0];
            float topleft_y = bb[1];
            float bottomright_x = bb[2];
            float bottomright_y = bb[3];

            drawPredInfo drawPredInfoForPalm = new drawPredInfo(1, 0, topleft_x, topleft_y, bottomright_x, bottomright_y);
            drawPredInfoForPalmsList.Add(drawPredInfoForPalm);
        }

        return new Tuple<List<drawPredInfo>, Mat>(drawPredInfoForPalmsList, palms);
    }

    //public Mat RunHandPoseDetector(Mat rgbaInput, Mat palms)
    //{
    //    if (rgbaInput == null || rgbaInput.empty() || palms == null || palms.empty())
    //    {
    //        Debug.Log("Input(s) null or empty"); // hasn't cause any issues yet
    //        return rgbaInput;
    //    }

    //    rgbaInput.copyTo(rgbaMat);

    //    Imgproc.cvtColor(rgbaMat, bgrMat, Imgproc.COLOR_RGBA2BGR);

    //    List<Mat> hands = new List<Mat>();

    //    // Estimate the pose of each hand
    //    for (int i = 0; i < palms.rows(); ++i)
    //    {
    //        // Handpose estimator inference
    //        Mat handpose = handPoseEstimator.infer(bgrMat, palms.row(i));

    //        if (!handpose.empty())
    //            hands.Add(handpose);
    //    }

    //    Imgproc.cvtColor(bgrMat, rgbaMat, Imgproc.COLOR_BGR2RGBA);

    //    foreach (var hand in hands)
    //    {
    //        handPoseEstimator.visualize(rgbaMat, hand, false, true);
    //        //hand.Dispose();
    //    }
    //    // dispose temporary hand mats
    //    foreach (var h in hands) h.Dispose();

    //    //Utils.matToTexture2D(rgbaMat, handPoseViz);
    //    //return handPoseViz;
    //    rgbaMat.copyTo(rgbaInput);
    //    return rgbaInput;
    //}

    public Tuple<Mat, Mat> RunHandPoseDetector(Mat rgbaInput, Mat palms)
    {
        List<Gesture> _unused;
        return RunHandPoseDetector(rgbaInput, palms, out _unused, visualize: true);
    }

    // New overload that also returns gestures
    public Tuple<Mat, Mat> RunHandPoseDetector(Mat rgbaInput, Mat palms, out List<Gesture> gestures, bool visualize = true)
    {
        gestures = new List<Gesture>();

        if (rgbaInput == null || rgbaInput.IsDisposed || rgbaInput.empty())
        {
            return new Tuple<Mat, Mat>(new Mat(), new Mat());
        }

        if (palms == null || palms.IsDisposed || palms.empty())
        {
            return new Tuple<Mat, Mat>(new Mat(), new Mat());
        }

        rgbaInput.copyTo(rgbaMat);

        List<Mat> handsList = new List<Mat>();
        for (int i = 0; i < palms.rows(); i++)
        {
            Mat palmRow = palms.row(i);
            Mat hand = handPoseEstimator.infer(rgbaMat, palmRow);

            if (hand != null && !hand.IsDisposed && !hand.empty())
            {
                handsList.Add(hand);

                Gesture g = ClassifyGestureFromHandResult(hand);
                gestures.Add(g);

                if (visualize)
                    handPoseEstimator.visualize(rgbaMat, hand, false, true, g.ToString());
            }
        };

        rgbaMat.copyTo(rgbaInput);

        //Mat handsMat = new Mat();
        Mat handsMat = OpenCVForUnity.UtilsModule.Converters.vector_Mat_to_Mat(handsList);

        return new Tuple<Mat, Mat>(handsMat, rgbaInput);
    }

    public Mat MatFromTexture(Texture2D rawVideoTexturesRGBA)
    {
        Mat _rgbaMat = new Mat(size, CvType.CV_8UC3);

        if (rawVideoTexturesRGBA != null)
        {
            Utils.texture2DToMat(rawVideoTexturesRGBA, _rgbaMat);
        }

        return _rgbaMat;
    }

    private Gesture ClassifyGestureFromHandResult(Mat handResult)
    {
        // MediaPipeHandPoseEstimator outputs a 132x1 vector:
        // [0:4]=bbox, [4:67]=21*3 screen landmarks, etc.
        if (handResult == null || handResult.IsDisposed || handResult.empty() || handResult.rows() < 132)
            return Gesture.Unknown;

        var data = handPoseEstimator.getData(handResult);
        Vector3[] lm = data.landmarks_screen;
        if (lm == null || lm.Length < 21)
            return Gesture.Unknown;

        // Indices: wrist=0, thumb=1..4, index=5..8, middle=9..12, ring=13..16, pinky=17..20
        int wrist = 0;

        bool indexExt = IsFingerExtended(lm, wrist, mcp: 5, pip: 6, tip: 8);
        bool middleExt = IsFingerExtended(lm, wrist, mcp: 9, pip: 10, tip: 12);
        bool ringExt = IsFingerExtended(lm, wrist, mcp: 13, pip: 14, tip: 16);
        bool pinkyExt = IsFingerExtended(lm, wrist, mcp: 17, pip: 18, tip: 20);

        bool thumbExt = IsThumbExtended(lm);

        int extCount = (indexExt ? 1 : 0) + (middleExt ? 1 : 0) + (ringExt ? 1 : 0) + (pinkyExt ? 1 : 0) + (thumbExt ? 1 : 0);

        // Pinch: thumb tip close to index tip relative to hand size
        float handScale = Vector2.Distance(ToV2(lm[0]), ToV2(lm[9])); // wrist to middle_mcp as rough scale
        float pinchDist = Vector2.Distance(ToV2(lm[4]), ToV2(lm[8]));
        if (handScale > 1e-3f && pinchDist < 0.25f * handScale)
            return Gesture.Pinch;

        if (extCount >= 5) return Gesture.OpenPalm;

        if (indexExt && !middleExt && !ringExt && !pinkyExt) return Gesture.Point;
        if (indexExt && middleExt && !ringExt && !pinkyExt) return Gesture.TwoFingers;

        // Thumbs up: thumb extended and above wrist (smaller y means "up" in image coords)
        if (thumbExt && !indexExt && !middleExt && !ringExt && !pinkyExt)
        {
            if (lm[4].y < lm[0].y) return Gesture.ThumbsUp;
        }

        if (extCount <= 1) return Gesture.Fist;

        return Gesture.Unknown;
    }

    private bool IsFingerExtended(Vector3[] lm, int wrist, int mcp, int pip, int tip)
    {
        // Use distance-to-wrist monotonic increase as a robust heuristic
        float dTip = Vector2.Distance(ToV2(lm[tip]), ToV2(lm[wrist]));
        float dPip = Vector2.Distance(ToV2(lm[pip]), ToV2(lm[wrist]));
        float dMcp = Vector2.Distance(ToV2(lm[mcp]), ToV2(lm[wrist]));

        // Extended if tip is significantly farther than pip & mcp
        return (dTip > dPip * 1.07f) && (dTip > dMcp * 1.15f);
    }

    private bool IsThumbExtended(Vector3[] lm)
    {
        // Thumb: compare wrist distances too (simple + surprisingly stable)
        float dTip = Vector2.Distance(ToV2(lm[4]), ToV2(lm[0]));
        float dMcp = Vector2.Distance(ToV2(lm[2]), ToV2(lm[0]));
        return dTip > dMcp * 1.2f;
    }

    private Vector2 ToV2(Vector3 v) => new Vector2(v.x, v.y);

    /// <summary>
    /// Raises the destroy event.
    /// </summary>
    void OnDestroy()
    {
        if (palmDetector != null)
            palmDetector.dispose();

        if (handPoseEstimator != null)
            handPoseEstimator.dispose();

        //Utils.setDebugMode(false);
    }
}
