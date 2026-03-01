using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using OpenCVForUnity.VideoModule;
using OpenCVForUnity.CoreModule;

using UnityEngine;
// using Debug = UnityEngine.Debug;

public class FaceTracker
{
    public KalmanFilter kalmanFilter;
    public Mat state; // State vector: [x, y, z, vx, vy, vz]
    public Mat measurement; // Measurement vector: [x, y, z]

    public FaceTracker(Vector3 initialPosition)
    {
        // Initialize Kalman filter with 6 state variables (position + velocity) and 3 measurement variables (position)
        kalmanFilter = new KalmanFilter(6, 3, 0, CvType.CV_32FC1);

        // Initialize the transition matrix
        Mat transitionMatrix = new Mat(6, 6, CvType.CV_32FC1);
        transitionMatrix.put(0, 0, new float[]
        {
            1, 0, 0, 1, 0, 0,
            0, 1, 0, 0, 1, 0,
            0, 0, 1, 0, 0, 1,
            0, 0, 0, 1, 0, 0,
            0, 0, 0, 0, 1, 0,
            0, 0, 0, 0, 0, 1
        });
        kalmanFilter.set_transitionMatrix(transitionMatrix);

        // Set initial state
        Mat statePreMat = kalmanFilter.get_statePre();
        statePreMat.put(0, 0, new float[] { initialPosition.x, initialPosition.y, initialPosition.z, 0, 0, 0 });
        Mat statePostMat = kalmanFilter.get_statePost();
        statePostMat.put(0, 0, new float[] { initialPosition.x, initialPosition.y, initialPosition.z, 0, 0, 0 });

        // Initialize the measurement matrix
        Mat measurementMatrix = new Mat(3, 6, CvType.CV_32FC1);
        Core.setIdentity(measurementMatrix);
        kalmanFilter.set_measurementMatrix(measurementMatrix);

        // Set process noise covariance
        Mat processNoiseCov = new Mat(6, 6, CvType.CV_32FC1);
        Core.setIdentity(processNoiseCov, Scalar.all(1e-4));
        kalmanFilter.set_processNoiseCov(processNoiseCov);

        // Set measurement noise covariance
        Mat measurementNoiseCov = new Mat(3, 3, CvType.CV_32FC1);
        Core.setIdentity(measurementNoiseCov, Scalar.all(1e-1));
        kalmanFilter.set_measurementNoiseCov(measurementNoiseCov);

        // Set error covariance post
        Mat errorCovPost = new Mat(6, 6, CvType.CV_32FC1);
        Core.setIdentity(errorCovPost, Scalar.all(0.1));
        kalmanFilter.set_errorCovPost(errorCovPost);

        // Initialize measurement matrix
        measurement = new Mat(3, 1, CvType.CV_32FC1);
        measurement.setTo(Scalar.all(0));
    }

    // Predicts the next position based on the current state
    public Vector3 PredictPosition()
    {
        Mat prediction = kalmanFilter.predict();
        return new Vector3(
            (float)prediction.get(0, 0)[0],
            (float)prediction.get(1, 0)[0],
            (float)prediction.get(2, 0)[0]
        );
    }

    // Updates the state using a new measurement (detected position)
    public void UpdateTrackerState(Vector3 position)
    {
        measurement.put(0, 0, new float[] { position.x, position.y, position.z });
        kalmanFilter.correct(measurement);
    }
}

public class FaceCubeScript : MonoBehaviour
{
    // public variables //

    // private variables //
    Renderer faceCubeRenderer;

    private Stopwatch eyeGazeStopwatch;
    private Stopwatch detectionStopwatch;
    private Stopwatch staleSubjectStopwatch;
    private long voiceAndEyeGazeCounter;
    private long totalEyeGazeTime;
    private long totalVoiceAndEyeGazeTime;
    private long eyeGazeCounter;
    private float percentEyeAndVoiceContact;
    private float percentEyeContact;

    private bool isTalking = false;
    private bool islooking = false;

    private int staleCounter = 0;
    private bool isSubject = false;

    private DateTime lastChangeToTrue = DateTime.Now;
    private DateTime lastChangeToFalse = DateTime.Now;

    [SerializeField]
    private int staleThreshold = 90;
    [SerializeField]
    private float percentEyeAndVoiceContactThreshold = 0.15f;
    [SerializeField]
    private float percentEyeContactThreshold = 0.25f;
    [SerializeField]
    private float staleDetectionLoseThreshold = 10000;

    [HideInInspector]
    public int cid;
    [HideInInspector]
    public float c;
    [HideInInspector]
    public double l;
    [HideInInspector]
    public double t;
    [HideInInspector]
    public double r;
    [HideInInspector]
    public double b;
    [HideInInspector]
    public double detection_width = 0;
    [HideInInspector]
    public double detection_height = 0;
    [HideInInspector]
    public double xCenter = 0;
    [HideInInspector]
    public double yCenter = 0;

    [HideInInspector]
    public string faceName;

    [HideInInspector]
    public Vector3 previousPosition;
    [HideInInspector]
    public Vector3 currentPosition;
    [HideInInspector]
    public Vector3 predictedPosition;
    [HideInInspector]
    public Vector3 velocity;
    [HideInInspector]
    public List<Vector3> positionHistory;
    [HideInInspector]
    public FaceTracker Tracker; // Kalman tracker associated with this face
    [HideInInspector]
    public float depth;
    [HideInInspector]
    public float predictedDepth;
    public int historyLimit = 20;

    [SerializeField]
    private bool visualizePredictedPosition = false;
    [SerializeField]
    private GameObject predictedPosVisualizerPrefab;
    private GameObject predictedPosVisualizer;
    private Renderer predictedPosVisualizerRenderer;

    // functions

    void Start()
    {
        faceCubeRenderer = GetComponent<Renderer>();

        eyeGazeStopwatch = new Stopwatch();
        detectionStopwatch = new Stopwatch();
        staleSubjectStopwatch = new Stopwatch();
        voiceAndEyeGazeCounter = 0;
        totalEyeGazeTime = 0;
        totalVoiceAndEyeGazeTime = 0;
        eyeGazeCounter = 0;
        percentEyeAndVoiceContact = 0;
        percentEyeContact = 0;
        detectionStopwatch.Start();

        faceName = transform.name;

        currentPosition = transform.position;
        predictedPosition = currentPosition;
        previousPosition = Vector3.zero;

        velocity = Vector3.zero;
        depth = currentPosition.z;
        positionHistory = new List<Vector3> { currentPosition };
        predictedDepth = predictedPosition.z;

        if (visualizePredictedPosition) 
        {
            predictedPosVisualizer = Instantiate(predictedPosVisualizerPrefab, predictedPosition, Quaternion.identity);
            predictedPosVisualizerRenderer = predictedPosVisualizer.GetComponent<Renderer>();
            predictedPosVisualizerRenderer.material.color = new Color(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
        }

        Tracker = new FaceTracker(currentPosition);
    }

    void Update()
    {
        staleCounter++;

        if (staleCounter > staleThreshold)
        {
            deleteFaceCube();
        }

        if (detectionStopwatch.ElapsedMilliseconds > 0)
        {
            percentEyeAndVoiceContact = (float)(totalVoiceAndEyeGazeTime + voiceAndEyeGazeCounter) / (float)detectionStopwatch.ElapsedMilliseconds;
            percentEyeContact = (float)(totalEyeGazeTime + eyeGazeCounter) / (float)detectionStopwatch.ElapsedMilliseconds;
        }

        if (percentEyeAndVoiceContact > percentEyeAndVoiceContactThreshold || percentEyeContact > percentEyeContactThreshold)
        {
            isSubject = true;
        }

        if (staleSubjectStopwatch.ElapsedMilliseconds > staleDetectionLoseThreshold)
        {
            isSubject = false;
        }

        // update predicted position data
        updatePredictedPosition();


        if (visualizePredictedPosition)
        {
            predictedPosVisualizer.transform.position = predictedPosition;
        }
    }

    private void updatePredictedPosition() 
    {
        if (previousPosition != Vector3.zero)
        {
            Vector3 direction = currentPosition - previousPosition;
            predictedPosition = currentPosition + direction;
        }
    }

    public Vector3 kalmanPredictedPosition()
    {
        Vector3 predPos = Tracker.PredictPosition();
        predictedPosition = predPos;
        predictedDepth = predPos.z;

        return predPos;
    }

    public void updateKalmanState(Vector3 currPos)
    {
        Tracker.UpdateTrackerState(currPos);
    }

    public void updateVelocityDepthCurrentAndHistoryPositions(Vector3 newPosition)
    {
        velocity = newPosition - currentPosition;
        depth = newPosition.z;
        currentPosition = newPosition;

        positionHistory.Add(currentPosition);
        if (positionHistory.Count > historyLimit)
        {
            positionHistory.RemoveAt(0);
        }

        // predictedPosition = currentPosition + velocity;// * Time.deltaTime;
        predictedPosition = calculatePredictedPosition();
        predictedDepth = predictedPosition.z;
    }

    public Vector3 calculatePredictedPosition()
    {
        // return currentPosition + velocity;// * Time.deltaTime;

        Vector3 averagePosition = Vector3.zero;
        float totalWeight = 0;
        float weight = 1.0f;

        for (int i = positionHistory.Count - 1; i >= 0; i--)
        {
            averagePosition += positionHistory[i] * weight;
            totalWeight += weight;
            weight *= 0.8f;
        }


        averagePosition /= totalWeight;

        Vector3 newPredictedPosition = averagePosition + velocity;

        return newPredictedPosition;
    }

    public Vector3 SmoothedPosition()
    {
        Vector3 averagePosition = Vector3.zero;
        foreach (Vector3 pos in positionHistory)
        {
            averagePosition += pos;
        }

        return averagePosition / positionHistory.Count;
    }

    private void deleteFaceCube()
    {
        Destroy(this.gameObject);

        if (visualizePredictedPosition)
        {
            Destroy(predictedPosVisualizer);
        }
    }

    public void EyeContactStarted()
    {
        eyeGazeStopwatch.Restart();
        staleSubjectStopwatch.Reset();
    }

    public void EyeContactMaintained()
    {
        if (!eyeGazeStopwatch.IsRunning)
        {
            eyeGazeStopwatch.Restart();
        }

        if (isTalking)
        {
            voiceAndEyeGazeCounter = eyeGazeStopwatch.ElapsedMilliseconds;
            eyeGazeCounter = eyeGazeStopwatch.ElapsedMilliseconds;
        }
        else
        {
            eyeGazeCounter = eyeGazeStopwatch.ElapsedMilliseconds;
        }
    }

    public void EyeContactLost()
    {
        eyeGazeStopwatch.Stop();
        staleSubjectStopwatch.Restart();
        voiceAndEyeGazeCounter = eyeGazeStopwatch.ElapsedMilliseconds;
        eyeGazeCounter = eyeGazeStopwatch.ElapsedMilliseconds;
        totalVoiceAndEyeGazeTime += voiceAndEyeGazeCounter;
        totalEyeGazeTime += eyeGazeCounter;
        eyeGazeCounter = 0;
        voiceAndEyeGazeCounter = 0;
    }

    public void setIsTalking(bool talking)
    {
        isTalking = talking;
    }

    public void setIslooking(bool looking)
    {
        islooking = looking;
    }

    public bool getIslooking()
    {
        return islooking;
    }

    public void setStaleCounter()
    {
        staleCounter = 0;
    }

    public bool getIsSubject()
    {
        return isSubject;
    }
}