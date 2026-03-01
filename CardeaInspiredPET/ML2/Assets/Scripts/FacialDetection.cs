using OpenCVForUnity.CoreModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;          
using System.Runtime.InteropServices;
using System.Text;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnityExample.DnnModel;
using System.Collections;


/// <summary>
/// Referring to https://github.com/opencv/opencv_zoo/tree/main/models/face_detection_yunet
/// </summary>
public class FacialDetection : MonoBehaviour
{
    public float conf_threshold = 0.9f;
    float nms_threshold = 0.3f;
    int topK = 5000;
    int backend = Dnn.DNN_BACKEND_OPENCV;
    int target = Dnn.DNN_TARGET_CPU;

    protected Scalar bBoxColor = new Scalar(0, 255, 0, 255);

    protected Scalar[] keyPointsColors = new Scalar[] {
        new Scalar(0, 0, 255, 255), // # right eye
        new Scalar(255, 0, 0, 255), // # left eye
        new Scalar(255, 255, 0, 255), // # nose tip
        new Scalar(0, 255, 255, 255), // # mouth right
        new Scalar(0, 255, 0, 255), // # mouth left
        new Scalar(255, 255, 255, 255) };

    public string model = "OpenCVForUnity/dnn/face_detection_yunet_2023mar.onnx";
    FaceDetectorYN detection_model;

    private Mat input_sizeMat;
    private Mat bgrMat;
    private Mat resultsMat;
    private Mat bgr = null;

    void Awake()
    {
        //if true, The error log of the Native side OpenCV will be displayed on the Unity Editor Console.
        Utils.setDebugMode(true);

        model = Utils.getFilePath(model);
        Debug.Log("model path: " + model);
        if (string.IsNullOrEmpty(model))
        {
            Debug.LogError(model + " is not loaded. Please read “StreamingAssets/OpenCVForUnity/dnn/setup_dnn_module.pdf” to make the necessary setup.");
        }
        else
        {
            if (!string.IsNullOrEmpty(model))
            {
                try
                {
                    Size input_size = new Size(1280,720);
                    detection_model = FaceDetectorYN.create(model, "", input_size, conf_threshold, nms_threshold, topK, backend, target);
                    
                    Debug.Log("FaceDetectorYN created OK");
                }
                catch (Exception e)
                {
                    Debug.LogError("FaceDetectorYN model init failed: " + e);
                }
            }
            else
            {
                Debug.LogError("model not found or string empty");
            }
        }
    }

    //protected virtual Mat preprocess(Mat image)
    //{
    //    int h = (int)image.height;
    //    int w = (int)image.width;

    //    if (input_sizeMat == null)
    //        input_sizeMat = new Mat(new Size(w, h), CvType.CV_8UC3);// [h, w]

    //    Imgproc.resize(image, input_sizeMat, new Size(w, h));

    //    return input_sizeMat;// [h, w, 3]
    //}

    //public virtual Mat infer(Mat image)
    public List<drawPredInfo> RunDetection(Mat image)
    {
        List<drawPredInfo> output = new List<drawPredInfo>();

        if (image == null || image.empty())
        {
            //Debug.LogError("RunDetection: image is null/empty");
            return output;
        }

        if (detection_model == null)
        {
            //Debug.LogError("RunDetection: detection_model is null (model init failed or Awake not run)");
            return output;
        }

        try
        {
            bgr = new Mat(image.rows(), image.cols(), CvType.CV_8UC3);
            // Convert depending on channels
            if (image.channels() == 4)
                Imgproc.cvtColor(image, bgr, Imgproc.COLOR_RGBA2BGR);
            else if (image.channels() == 3)
                image.copyTo(bgr);
            else
            {
                //Debug.LogError($"RunDetection: unexpected channels={image.channels()}");
                return output;
            }

            //Debug.Log($"BGR: {bgr.cols()}x{bgr.rows()} ch={bgr.channels()} type={bgr.type()} mean={Core.mean(bgr)}");
        }
        catch (Exception e)
        {
            //Debug.LogError("bgr conversion failed: " + e);
            return output;
        }

        // Preprocess
        //Mat input_blob = preprocess(bgr);

        // Forward
        Mat results = new Mat();
        try
        {
            detection_model.setInputSize(new Size(bgr.cols(), bgr.rows()));
            //Debug.Log("Model input size changed");
            detection_model.detect(bgr, results);

            //Debug.Log($"YuNet results: empty={results.empty()} rows={results.rows()} cols={results.cols()}");
        }
        catch (Exception e)
        {
            //Debug.LogError("model inference failed: " + e);
            return output;
        }

        //// Postprocess
        //// scale_boxes
        //try
        //{
        //    float x_factor = image.width() / (float)bgr.width;
        //    float y_factor = image.height() / (float)bgr .height;

        //    for (int i = 0; i < results.rows(); ++i)
        //    {
        //        float[] results_arr = new float[14];
        //        results.get(i, 0, results_arr);
        //        for (int j = 0; j < 14; ++j)
        //        {
        //            if (j % 2 == 0)
        //            {
        //                results_arr[j] = results_arr[j] * x_factor;
        //            }
        //            else
        //            {
        //                results_arr[j] = results_arr[j] * y_factor;
        //            }
        //        }

        //        results.put(i, 0, results_arr);
        //    }
        //}
        //catch (Exception e)
        //{
        //    Debug.LogError("error in scaling results: " + e);
        //}
        

        // get detection data
        try
        {
            DetectionData[] data = getData(results);

            //Debug.Log("Post DetectionData call");

            foreach (var d in data)
            {
                float score = d.score;
                if (score < conf_threshold) continue;

                float left = d.xy.x;
                float top = d.xy.y;
                float right = d.xy.x + d.wh.x;
                float bottom = d.xy.y + d.wh.y;

                output.Add(new drawPredInfo(0, score, left, top, right, bottom));
            }

            //Debug.Log("RunDetection is about to return fine!");
        }
        catch (Exception e)
        {
            //Debug.LogError("error in scaling results: " + e);
            return output;
        }
        

        //return results;
        return output;
    }


    //private void OnDestroy()
    //{
    //    bgrMat?.Dispose();
    //    resultsMat?.Dispose();
    //    input_sizeMat?.Dispose();
    //    detection_model?.Dispose();
    //}


    protected virtual Mat postprocess(Mat output_blob)
    {
        return output_blob;
    }

    public virtual void visualize(Mat image, Mat results, bool print_results = false, bool isRGB = false)
    {
        if (image.IsDisposed)
            return;

        if (results.empty() || results.cols() < 15)
            return;

        DetectionData[] data = getData(results);

        foreach (var d in data.Reverse())
        {
            float left = d.xy.x;
            float top = d.xy.y;
            float right = d.xy.x + d.wh.x;
            float bottom = d.xy.y + d.wh.y;
            float score = d.score;

            Scalar bbc = bBoxColor;
            Scalar bbcolor = isRGB ? bbc : new Scalar(bbc.val[2], bbc.val[1], bbc.val[0], bbc.val[3]);

            Imgproc.rectangle(image, new Point(left, top), new Point(right, bottom), bbcolor, 2);

            string label = String.Format("{0:0.0000}", score);
            int[] baseLine = new int[1];
            Size labelSize = Imgproc.getTextSize(label, Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, 1, baseLine);

            top = Mathf.Max((float)top, (float)labelSize.height);
            Imgproc.rectangle(image, new Point(left, top - labelSize.height),
                new Point(left + labelSize.width, top + baseLine[0]), bbcolor, Core.FILLED);
            Imgproc.putText(image, label, new Point(left, top), Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, new Scalar(0, 0, 0, 255), 1, Imgproc.LINE_AA);

            // draw landmark points
            Imgproc.circle(image, new Point(d.rightEye.x, d.rightEye.y), 2,
                isRGB ? keyPointsColors[0] : new Scalar(keyPointsColors[0].val[2], keyPointsColors[0].val[1], keyPointsColors[0].val[0], keyPointsColors[0].val[3]), 2);
            Imgproc.circle(image, new Point(d.leftEye.x, d.leftEye.y), 2,
                isRGB ? keyPointsColors[1] : new Scalar(keyPointsColors[1].val[2], keyPointsColors[1].val[1], keyPointsColors[1].val[0], keyPointsColors[1].val[3]), 2);
            Imgproc.circle(image, new Point(d.nose.x, d.nose.y), 2,
                isRGB ? keyPointsColors[2] : new Scalar(keyPointsColors[2].val[2], keyPointsColors[2].val[1], keyPointsColors[2].val[0], keyPointsColors[2].val[3]), 2);
            Imgproc.circle(image, new Point(d.rightMouth.x, d.rightMouth.y), 2,
                isRGB ? keyPointsColors[3] : new Scalar(keyPointsColors[3].val[2], keyPointsColors[3].val[1], keyPointsColors[3].val[0], keyPointsColors[3].val[3]), 2);
            Imgproc.circle(image, new Point(d.leftMouth.x, d.leftMouth.y), 2,
                isRGB ? keyPointsColors[4] : new Scalar(keyPointsColors[4].val[2], keyPointsColors[4].val[1], keyPointsColors[4].val[0], keyPointsColors[4].val[3]), 2);
        }

        // Print results
        if (print_results)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < data.Length; ++i)
            {
                var d = data[i];
                float left = d.xy.x;
                float top = d.xy.y;
                float right = d.xy.x + d.wh.x;
                float bottom = d.xy.y + d.wh.y;
                float score = d.score;

                sb.AppendLine(String.Format("-----------face {0}-----------", i + 1));
                sb.AppendLine(String.Format("score: {0:0.0000}", score));
                sb.AppendLine(String.Format("box: {0:0} {1:0} {2:0} {3:0}", left, top, right, bottom));
                sb.Append("landmarks: ");
                sb.Append(String.Format("{0:0} {1:0} ", d.rightEye.x, d.rightEye.y));
                sb.Append(String.Format("{0:0} {1:0} ", d.leftEye.x, d.leftEye.y));
                sb.Append(String.Format("{0:0} {1:0} ", d.nose.x, d.nose.y));
                sb.Append(String.Format("{0:0} {1:0} ", d.rightMouth.x, d.rightMouth.y));
                sb.Append(String.Format("{0:0} {1:0} ", d.leftMouth.x, d.leftMouth.y));

                sb.AppendLine();
            }

            Debug.Log(sb);
        }
    }

    public virtual void dispose()
    {
        if (detection_model != null)
            detection_model.Dispose();

        if (input_sizeMat != null)
            input_sizeMat.Dispose();

        input_sizeMat = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DetectionData
    {
        // Bounding box
        public readonly Vector2 xy;
        public readonly Vector2 wh;

        // Key points
        public readonly Vector2 rightEye;
        public readonly Vector2 leftEye;
        public readonly Vector2 nose;
        public readonly Vector2 rightMouth;
        public readonly Vector2 leftMouth;

        // Confidence score [0, 1]
        public readonly float score;

        // sizeof(DetectionData)
        public const int Size = 15 * sizeof(float);

        public DetectionData(Vector2 xy, Vector2 wh, Vector2 rightEye, Vector2 leftEye, Vector2 nose, Vector2 rightMouth, Vector2 leftMouth, float score)
        {
            this.xy = xy;
            this.wh = wh;
            this.rightEye = rightEye;
            this.leftEye = leftEye;
            this.nose = nose;
            this.rightMouth = rightMouth;
            this.leftMouth = leftMouth;
            this.score = score;
        }

        public override string ToString()
        {
            return "xy:" + xy + " wh:" + wh + " rightEye:" + rightEye + " leftEye:" + leftEye + " nose:" + nose + " rightMouth:" + rightMouth + " leftMouth:" + leftMouth + " score:" + score;
        }
    };

    public virtual DetectionData[] getData(Mat results)
    {
        if (results.empty())
            return new DetectionData[0];

        var dst = new DetectionData[results.rows()];
        OpenCVForUnity.UtilsModule.MatUtils.copyFromMat(results, dst);

        return dst;
    }
}