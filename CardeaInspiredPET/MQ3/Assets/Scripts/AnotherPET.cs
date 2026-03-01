using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.IO;

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;

using UnityRect = UnityEngine.Rect;
using Debug = UnityEngine.Debug;


public class AnotherPET : MonoBehaviour
{
    [SerializeField]
    private GameObject evaluatAR;
    private EvaluatAR evaluatARScript;

    [SerializeField]
    private GameObject facialDetector;
    private FacialDetection facialDetectorScript;

    [SerializeField]
    private GameObject handPoseDetector;
    private HandPoseDetection handPoseDetectorScript;
    private Texture2D handPoseViz = null;
    private Mat sharedRgba;

    private Texture2D currFrame = null;
    private int currFrameWidth;
    private int currFrameHeight;

    [SerializeField]
    private GameObject debugQuad;
    private Renderer debugQuadRenderer;


    // face tracking logic
    private int _nextTrackId = 1;
    private class FaceTrack
    {
        public int id;
        public UnityRect bboxPx;                // pixel coords
        public Vector2 centerPx;
        public float lastSeenTime;
        public float cooldownUntilTime;    // Time.time until which this face is "cooling down"
        public bool obfuscated;

        // gesture stability
        public HandPoseDetection.Gesture lastGesture = HandPoseDetection.Gesture.Unknown;
        public int sameGestureCount = 0;
    }
    private Dictionary<int, FaceTrack> _tracks = new Dictionary<int, FaceTrack>();
    public float faceTrackMaxCenterDistPx = 120f;
    public float faceTrackStaleSeconds = 0.75f;
    public float palmToFaceMaxCenterDistPx = 220f;
    public float cooldownSeconds = 0.0f;
    public HandPoseDetection.Gesture obfuscateGesture = HandPoseDetection.Gesture.OpenPalm;
    public HandPoseDetection.Gesture clearGesture = HandPoseDetection.Gesture.TwoFingers;
    public bool requireStableGestureForToggle = false;
    public int stableGestureFrames = 2;
    public int faceBlurKernel = 31;          // must be odd
    public bool expandBlurBox = true;
    public float blurBoxExpandFrac = 0.12f;  // 12% padding around the face box

    // Logging helpers 
    private Stopwatch _trialSw;

    private class FaceLog
    {
        public int face_id;
        public float x, y, w, h;
        public float cx, cy;
        public bool active;
        public bool obfuscated;

        public int matched_palm_idx;

        public string gesture;         // detected gesture this frame for this face (or "Unknown")
        public bool stableEnough;      // whether it passed stability gate
        public int stableCount;        // track.sameGestureCount at this moment
        public int stableRequired;     // stableGestureFrames

        public string toggle;          // "on" | "off" | "none"
        public string toggleReason;    // "gesture" | "unstable" | "unknownGesture" | "noPalmMatched" | ...
    }

    private class FrameLog
    {
        public long t_ms;

        public int faces_total, faces_active, faces_invalid;
        public int palms_total, pairs_total;

        public float ms_face, ms_palm, ms_pose, ms_blur, ms_total;

        public List<FaceLog> faces;
    }

    void Start()
    {
        evaluatARScript = evaluatAR.GetComponent<EvaluatAR>();
        debugQuadRenderer = debugQuad.GetComponent<Renderer>();
        facialDetectorScript = facialDetector.GetComponent<FacialDetection>();
        handPoseDetectorScript = handPoseDetector.GetComponent<HandPoseDetection>();

        handPoseViz = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        sharedRgba = new Mat(1280, 720, CvType.CV_8UC3);
        EnsureVizBuffers(1280, 720);
    }

    void Update()
    {
        // string logData = "{";
        long tMs = _trialSw != null ? _trialSw.ElapsedMilliseconds : 0;
        var frameLog = new FrameLog
        {
            t_ms = tMs,
            faces = new List<FaceLog>()
        };

        var swTotal = Stopwatch.StartNew();

        if (currFrame != null)
        {
            evaluatARScript.setCurrentCameraCapture(currFrame);
            EnsureVizBuffers(currFrameWidth, currFrameHeight);

            // Convert Texture2D -> shared RGBA Mat ONCE
            Utils.texture2DToMat(currFrame, sharedRgba);

            // run facial detection to get 2D coords of faces
            var sw = Stopwatch.StartNew();
            var detectedFacesMetaData = facialDetectorScript.RunDetection(sharedRgba);
            sw.Stop();
            frameLog.ms_face = (float)sw.Elapsed.TotalMilliseconds;

            //if (detectedFacesMetaData.Count == 0)
            //{
            //    Debug.Log("No detected face");
            //}

            // distance based tracking of faces between consecutive frames
            List<UnityRect> faceBBoxesPx = ToUnityRects(detectedFacesMetaData);
            UpdateFaceTracks(faceBBoxesPx);

            // determine faces that are out of the cooldown period (a.k.a valid faces)
            List<FaceTrack> activeFaces = new List<FaceTrack>();
            List<FaceTrack> invalidFaces = new List<FaceTrack>();

            // string keyLine = "\"FaceDetections\": {";
            // string valueLines = "";
            foreach (var kv in _tracks)
            {
                FaceTrack t = kv.Value;
                string decision = "";

                if (Time.time >= t.cooldownUntilTime)
                {
                    activeFaces.Add(t);
                    decision = "activeFace";
                }
                else
                {
                    invalidFaces.Add(t);    // in cool down rn
                    decision = "invalidFace";
                }

                // string id = "\"" + t.id + "\" : {";
                // string faceDecision = "\"decision\": \"" + decision + "\" |";
                // string bboxPx = "\"bboxPx\": \"(" + t.bboxPx.x + " | " + t.bboxPx.y + " | " + t.bboxPx.width  + " | " + t.bboxPx.height + ")\" |";
                // string centerPx = "\"centerPx\": \"(" + t.centerPx.x + " | " + t.centerPx.y + ")\" |";
                // string lastSeenTime = "\"lastSeenTime\": \"" + t.lastSeenTime + "\" |";
                // string cooldownUntilTime = "\"cooldownUntilTime\": \"" + t.cooldownUntilTime + "\" |";
                // string obfuscated = "\"obfuscated\": \"" + t.obfuscated + "\" |";
                // string lastGesture = "\"lastGesture\": \"" + t.lastGesture + "\" |";
                // string sameGestureCount = "\"sameGestureCount\": \"" + t.sameGestureCount + "\" }";

                // string valueLine = id + faceDecision + bboxPx + centerPx + lastSeenTime + cooldownUntilTime + obfuscated + lastGesture + sameGestureCount + "|";
                // valueLines += valueLine;
            }
            // logData = logData + keyLine + valueLines + "} |";
            frameLog.faces_total = _tracks.Count;
            frameLog.faces_active = activeFaces.Count;
            frameLog.faces_invalid = invalidFaces.Count;


            sw = Stopwatch.StartNew();
            ApplyObfuscationForAllObfuscatedTracks();
            sw.Stop();
            frameLog.ms_blur = (float)sw.Elapsed.TotalMilliseconds;

            if (activeFaces.Count == 0)
            {
                // Debug.Log("Post facial detection; no face OR no 'valid' face to run hand pose detection on");
                DrawTrackingOverlays(sharedRgba, invalidFaces, activeFaces, new List<UnityRect>(), new Dictionary<int, int>());

                Utils.matToTexture2D(sharedRgba, handPoseViz);
                handPoseViz.Apply();
                debugQuadRenderer.material.mainTexture = handPoseViz;

                // logData += "}";
                // logHelper(logData);
                foreach (var kv in _tracks)
                {
                    var t = kv.Value;
                    frameLog.faces.Add(new FaceLog
                    {
                        face_id = t.id,
                        x = t.bboxPx.x,
                        y = t.bboxPx.y,
                        w = t.bboxPx.width,
                        h = t.bboxPx.height,
                        cx = t.centerPx.x,
                        cy = t.centerPx.y,
                        active = (Time.time >= t.cooldownUntilTime),
                        obfuscated = t.obfuscated,
                        matched_palm_idx = -1,
                        gesture = "None",
                        stableEnough = false,
                        stableCount = t.sameGestureCount,
                        stableRequired = stableGestureFrames,
                        toggle = "none",
                        toggleReason = "noActiveFaces"
                    });
                }
                swTotal.Stop();
                frameLog.ms_total = (float)swTotal.Elapsed.TotalMilliseconds;
                logHelper(frameLog);

                return;
            }

            // detect hands
            sw = Stopwatch.StartNew();
            var detectedPalmsMetaData = handPoseDetectorScript.RunPalmDetector(sharedRgba);
            List<drawPredInfo> palmsBoxes = detectedPalmsMetaData.Item1;
            Mat palmsMat = detectedPalmsMetaData.Item2;
            sw.Stop();
            frameLog.ms_palm = (float)sw.Elapsed.TotalMilliseconds;

            // check which hands belong to which valid face (distance of centers of topleft bottomright coords of faces and hands)
            List<UnityRect> palmBBoxesPx = ToUnityRects(palmsBoxes);
            frameLog.palms_total = palmBBoxesPx.Count;

            // keyLine = "\"HandDetections\": {";
            // valueLines = "";
            // int plamIDCounter = 0;
            // foreach(UnityRect p in palmBBoxesPx)
            // {
            //     string id = "\"" + plamIDCounter + "\" : {";
            //     string palmBboxPx = "\"palmBboxPx\": \"(" + p.x + " | " + p.y + " | " + p.width + " | " + p.height + ")\" }";

            //     string valueLine = id + palmBboxPx + "|";
            //     valueLines += valueLine;

            //     plamIDCounter++;
            // }
            // logData = logData + keyLine + valueLines + "} |";

            Dictionary<int, int> faceIdToPalmIndex = MatchPalmsToFaces(activeFaces, palmBBoxesPx);
            frameLog.pairs_total = faceIdToPalmIndex.Count;
            // keyLine = "\"HandFaceMapping\": {";
            // valueLines = "";
            // foreach (KeyValuePair<int, int> kvp in faceIdToPalmIndex)
            // {
            //     string id = "\"" + kvp.Key + "\" : {";
            //     string mappedHand = "\"mappedHand\": \"" + kvp.Value + "\" }";

            //     string valueLine = id + mappedHand + "|";
            //     valueLines += valueLine;
            // }
            // logData = logData + keyLine + valueLines + "} |";

            if (faceIdToPalmIndex.Count == 0)
            {
                // Debug.Log("Post palm detection; no palms/hands were detected that can be associated with a valid face");
                DrawTrackingOverlays(sharedRgba, invalidFaces, activeFaces, palmBBoxesPx, faceIdToPalmIndex);

                Utils.matToTexture2D(sharedRgba, handPoseViz);
                handPoseViz.Apply();
                debugQuadRenderer.material.mainTexture = handPoseViz;

                // logData += "}";
                // logHelper(logData);
                foreach (var kv in _tracks)
                {
                    var t = kv.Value;
                    frameLog.faces.Add(new FaceLog
                    {
                        face_id = t.id,
                        x = t.bboxPx.x,
                        y = t.bboxPx.y,
                        w = t.bboxPx.width,
                        h = t.bboxPx.height,
                        cx = t.centerPx.x,
                        cy = t.centerPx.y,
                        active = (Time.time >= t.cooldownUntilTime),
                        obfuscated = t.obfuscated,
                        matched_palm_idx = -1,
                        gesture = "None",
                        stableEnough = false,
                        stableCount = t.sameGestureCount,
                        stableRequired = stableGestureFrames,
                        toggle = "none",
                        toggleReason = "noPalmMatched"
                    });
                }
                swTotal.Stop();
                frameLog.ms_total = (float)swTotal.Elapsed.TotalMilliseconds;
                logHelper(frameLog);

                palmsMat?.Dispose();
                return;
            }

            // make a mat for hands associated only with valid faces 
            List<int> orderedFaceIds = new List<int>();
            List<Mat> selectedPalmRows = new List<Mat>();
            foreach (var pair in faceIdToPalmIndex)
            {
                int faceId = pair.Key;
                int palmIdx = pair.Value;

                Mat palmRow = palmsMat.row(palmIdx);
                orderedFaceIds.Add(faceId);
                selectedPalmRows.Add(palmRow);
            }

            Mat selectedPalmsMat = new Mat();
            if (selectedPalmRows != null && selectedPalmRows.Count > 0)
            {
                // Clone to avoid issues if any rows are views (row() results) or get disposed elsewhere.
                List<Mat> rows = new List<Mat>(selectedPalmRows.Count);
                foreach (var r in selectedPalmRows)
                {
                    if (r == null || r.IsDisposed || r.empty()) continue;
                    rows.Add(r.clone());
                }
                Core.vconcat(rows, selectedPalmsMat);

                // Clean up clones
                foreach (var r in rows) r.Dispose();
            }

            // determine gestures for each valid face's hand
            sw = Stopwatch.StartNew();
            List<HandPoseDetection.Gesture> gestures;
            var handPoseMetaData = handPoseDetectorScript.RunHandPoseDetector(sharedRgba, selectedPalmsMat, out gestures, visualize: true);
            Mat handsMat = handPoseMetaData.Item1;
            sharedRgba = handPoseMetaData.Item2;
            sw.Stop();
            frameLog.ms_pose = (float)sw.Elapsed.TotalMilliseconds;

            // Safety: gestures count should match selected palms
            int n = Mathf.Min(orderedFaceIds.Count, gestures.Count);
            HashSet<int> facesWithPalms = new HashSet<int>(faceIdToPalmIndex.Keys);

            // keyLine = "\"FaceGestureMapping\": {";
            // valueLines = "";
            for (int i = 0; i < n; i++)
            {
                int faceId = orderedFaceIds[i];
                FaceTrack track = _tracks[faceId];
                HandPoseDetection.Gesture g = gestures[i];

                // string id = "";
                // string gesture = "";
                // string valueLine = "";
                string toggle = "none";
                string gStr = g.ToString();

                if (g == HandPoseDetection.Gesture.Unknown)
                {
                    ResetGestureStability(track, g);

                    // id = "\"" + faceId + "\" : {";
                    // gesture = "\"gesture\": \"Unkown\" }";
                    // valueLine = id + gesture + "|";
                    // valueLines += valueLine;
                    frameLog.faces.Add(new FaceLog
                    {
                        face_id = faceId,
                        x = track.bboxPx.x,
                        y = track.bboxPx.y,
                        w = track.bboxPx.width,
                        h = track.bboxPx.height,
                        cx = track.centerPx.x,
                        cy = track.centerPx.y,
                        active = true,
                        obfuscated = track.obfuscated,
                        matched_palm_idx = faceIdToPalmIndex[faceId],
                        gesture = "Unknown",
                        stableEnough = false,
                        stableCount = track.sameGestureCount,
                        stableRequired = stableGestureFrames,
                        toggle = "none",
                        toggleReason = "unknownGesture"
                    });

                    continue;
                }

                bool stableEnough = true;
                if (requireStableGestureForToggle)
                {
                    stableEnough = UpdateGestureStability(track, g);
                }
                else
                {
                    track.lastGesture = g;
                    track.sameGestureCount = 999;
                }

                if (!stableEnough)
                {
                    // id = "\"" + faceId + "\" : {";
                    // gesture = "\"gesture\": \"" + g + "\" }";
                    // valueLine = id + gesture + "|";
                    // valueLines += valueLine;
                    frameLog.faces.Add(new FaceLog
                    {
                        face_id = faceId,
                        x = track.bboxPx.x,
                        y = track.bboxPx.y,
                        w = track.bboxPx.width,
                        h = track.bboxPx.height,
                        cx = track.centerPx.x,
                        cy = track.centerPx.y,
                        active = true,
                        obfuscated = track.obfuscated,
                        matched_palm_idx = faceIdToPalmIndex[faceId],   // safe because this loop is over orderedFaceIds
                        gesture = g.ToString(),
                        stableEnough = false,
                        stableCount = track.sameGestureCount,
                        stableRequired = stableGestureFrames,
                        toggle = "none",
                        toggleReason = "unstable"
                    });

                    continue;
                }

                bool didToggle = false;
                toggle = "none";
                string reason = "gestureNotMapped";

                // Turn ON
                if (!track.obfuscated && g == obfuscateGesture)
                {
                    track.obfuscated = true;
                    didToggle = true;
                    SetFaceObfuscation(faceId, true, track.bboxPx);
                }
                // Turn OFF
                else if (track.obfuscated && g == clearGesture)
                {
                    track.obfuscated = false;
                    didToggle = true;
                    SetFaceObfuscation(faceId, false, track.bboxPx);
                }

                if (didToggle)
                {
                    track.cooldownUntilTime = Time.time + cooldownSeconds;

                    // reset stability so you don’t immediately re-trigger after cooldown ends
                    track.lastGesture = HandPoseDetection.Gesture.Unknown;
                    track.sameGestureCount = 0;

                    toggle = track.obfuscated ? "on" : "off";
                    reason = "gesture";
                }
                else
                {
                    toggle = "none";
                    reason = "gestureNoToggle";
                }

                // id = "\"" + faceId + "\" : {";
                // gesture = "\"gesture\": \"" + g + "\" |";
                // string obfuscated = "\"obfuscated\": \"" + track.obfuscated + "\" }";
                // valueLine = id + gesture + obfuscated + "|";
                // valueLines += valueLine;
                frameLog.faces.Add(new FaceLog
                {
                    face_id = faceId,
                    x = track.bboxPx.x,
                    y = track.bboxPx.y,
                    w = track.bboxPx.width,
                    h = track.bboxPx.height,
                    cx = track.centerPx.x,
                    cy = track.centerPx.y,
                    active = true,
                    obfuscated = track.obfuscated,
                    matched_palm_idx = faceIdToPalmIndex[faceId],
                    gesture = g.ToString(),
                    stableEnough = true,
                    stableCount = track.sameGestureCount,
                    stableRequired = stableGestureFrames,
                    toggle = toggle,
                    toggleReason = reason
                });
            }
            // logData = logData + keyLine + valueLines + "} |"; 

            DrawTrackingOverlays(sharedRgba, invalidFaces, activeFaces, palmBBoxesPx, faceIdToPalmIndex);

            Utils.matToTexture2D(sharedRgba, handPoseViz);
            handPoseViz.Apply();
            debugQuadRenderer.material.mainTexture = handPoseViz;

            selectedPalmsMat.Dispose();
            handsMat.Dispose();
            palmsMat.Dispose();

            foreach (var kv in _tracks)
            {
                var t = kv.Value;
                if (t == null) continue;
                if (!facesWithPalms.Contains(t.id))
                {
                    bool isActive = (Time.time >= t.cooldownUntilTime);
                    frameLog.faces.Add(MakeFaceLog(t, isActive, -1, "Unknown", "none"));
                }
            }
        }

        // logData += "}";
        // logHelper(logData);
        swTotal.Stop();
        frameLog.ms_total = (float)swTotal.Elapsed.TotalMilliseconds;
        logHelper(frameLog);
    }

    // private void logHelper(string logData)
    // {
    //     if (evaluatARScript.dataCollectionMode)
    //     {
    //         logData = "";
    //         evaluatARScript.writeToLogsFile("");
    //     }
    //     else
    //     {
    //         evaluatARScript.writeToLogsFile(logData);
    //     }
    // }
    private void logHelper(FrameLog frameLog)
    { // TODO: adapt this to the FrameLog structure to our logging structure
        if (evaluatARScript.dataCollectionMode)
        {
            evaluatARScript.writeToLogsFile("");
        }
        else
        {
            // Build custom log string from FrameLog

            var sb = new StringBuilder(1024);

            // helpers to avoid commas and quotes issues
            string Q(string s)
            {
                if (s == null) return "";
                // remove commas to keep CSV safe; replace quotes to avoid breaking your format
                return s.Replace("\"", "'").Replace(",", ";");
            }
            string B(bool v) => v ? "1" : "0";
            string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

            sb.Append("{");
            sb.Append("\"t_ms\":\"").Append(frameLog.t_ms).Append("\"|");
            sb.Append("\"faces_total\":\"").Append(frameLog.faces_total).Append("\"|");
            sb.Append("\"faces_active\":\"").Append(frameLog.faces_active).Append("\"|");
            sb.Append("\"faces_invalid\":\"").Append(frameLog.faces_invalid).Append("\"|");
            sb.Append("\"palms_total\":\"").Append(frameLog.palms_total).Append("\"|");
            sb.Append("\"pairs_total\":\"").Append(frameLog.pairs_total).Append("\"|");

            sb.Append("\"ms_face\":\"").Append(F(frameLog.ms_face)).Append("\"|");
            sb.Append("\"ms_palm\":\"").Append(F(frameLog.ms_palm)).Append("\"|");
            sb.Append("\"ms_pose\":\"").Append(F(frameLog.ms_pose)).Append("\"|");
            sb.Append("\"ms_blur\":\"").Append(F(frameLog.ms_blur)).Append("\"|");
            sb.Append("\"ms_total\":\"").Append(F(frameLog.ms_total)).Append("\"|");

            sb.Append("\"faces\":[");
            for (int i = 0; i < frameLog.faces.Count; i++)
            {
                var x = frameLog.faces[i];
                sb.Append("{");
                sb.Append("\"id\":\"").Append(x.face_id).Append("\"|");
                sb.Append("\"bbox\":\"(").Append(F(x.x)).Append("|").Append(F(x.y)).Append("|").Append(F(x.w)).Append("|").Append(F(x.h)).Append(")\"|");
                sb.Append("\"c\":\"(").Append(F(x.cx)).Append("|").Append(F(x.cy)).Append(")\"|");
                sb.Append("\"active\":\"").Append(B(x.active)).Append("\"|");
                sb.Append("\"obf\":\"").Append(B(x.obfuscated)).Append("\"|");
                sb.Append("\"palm\":\"").Append(x.matched_palm_idx).Append("\"|");
                sb.Append("\"g\":\"").Append(Q(x.gesture)).Append("\"|");
                sb.Append("\"stable\":\"").Append(B(x.stableEnough)).Append("\"|");
                sb.Append("\"stableCnt\":\"").Append(x.stableCount).Append("\"|");
                sb.Append("\"stableReq\":\"").Append(x.stableRequired).Append("\"|");
                sb.Append("\"toggle\":\"").Append(Q(x.toggle)).Append("\"|");
                sb.Append("\"why\":\"").Append(Q(x.toggleReason)).Append("\"");
                sb.Append("}");

                if (i < frameLog.faces.Count - 1) sb.Append("|"); // pipe separator between face objects
            }
            sb.Append("]");

            sb.Append("}");

            evaluatARScript.writeToLogsFile(sb.ToString());
        }
    }

    private FaceLog MakeFaceLog(FaceTrack t, bool active, int matchedPalmIdx, string gestureStr, string toggle)
    {
        return new FaceLog
        {
            face_id = t.id,
            x = t.bboxPx.x,
            y = t.bboxPx.y,
            w = t.bboxPx.width,
            h = t.bboxPx.height,
            cx = t.centerPx.x,
            cy = t.centerPx.y,
            active = active,
            obfuscated = t.obfuscated,
            matched_palm_idx = matchedPalmIdx,
            gesture = gestureStr,
            toggle = toggle
        };
    }

    public void setCurrFrame(Texture2D frame, int frameWidth, int frameHeight)
    {
        currFrame = frame;
        currFrameWidth = frameWidth;
        currFrameHeight = frameHeight;
        //_frameSeq++;
    }

    private Scalar ColorForFaceId(int faceId)
    {
        // Simple hash -> 3 channels in [50..255] to avoid too-dark colors
        int h = faceId * 1103515245 + 12345;
        byte c1 = (byte)(50 + (h & 0x7F));          // 50..177
        byte c2 = (byte)(80 + ((h >> 7) & 0x7F));   // 80..207
        byte c3 = (byte)(110 + ((h >> 14) & 0x7F)); // 110..237

        // OpenCVForUnity Scalar is typically (b,g,r,a)
        return new Scalar(c1, c2, c3, 255);
    }

    private void DrawBoxes(Mat rgba, List<drawPredInfo> boxes, Scalar color)
    {
        if (rgba == null || rgba.empty() || boxes == null) return;

        int imgW = rgba.cols();
        int imgH = rgba.rows();

        foreach (var b in boxes)
        {
            // Assuming drawPredInfo stores left/top/right/bottom
            // If your member names differ, update these four lines.
            float left = (float)b.left;
            float top = (float)b.top;
            float right = (float)b.right;
            float bottom = (float)b.bottom;

            int x1 = Mathf.Clamp(Mathf.RoundToInt(left), 0, imgW - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(top), 0, imgH - 1);
            int x2 = Mathf.Clamp(Mathf.RoundToInt(right), 0, imgW - 1);
            int y2 = Mathf.Clamp(Mathf.RoundToInt(bottom), 0, imgH - 1);

            Imgproc.rectangle(
                rgba,
                new Point(x1, y1),
                new Point(x2, y2),
                color,
                2
            );
        }
    }

    private void DrawBoxes(Mat rgba, List<FaceTrack> tracks, Scalar color)
    {
        if (rgba == null || rgba.empty() || tracks == null) return;

        int imgW = rgba.cols();
        int imgH = rgba.rows();

        foreach (var t in tracks)
        {
            if (t == null) continue;

            UnityRect r = t.bboxPx;

            // Common UnityRect fields are x, y, width, height (top-left origin in pixels).
            // If your UnityRect uses different names, adjust these 4 lines accordingly.
            float left = (float)r.x;
            float top = (float)r.y;
            float right = (float)(r.x + r.width);
            float bottom = (float)(r.y + r.height);

            int x1 = Mathf.Clamp(Mathf.RoundToInt(left), 0, imgW - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(top), 0, imgH - 1);
            int x2 = Mathf.Clamp(Mathf.RoundToInt(right), 0, imgW - 1);
            int y2 = Mathf.Clamp(Mathf.RoundToInt(bottom), 0, imgH - 1);

            // Ensure correct ordering in case width/height are negative or coords are swapped
            if (x2 < x1) { int tmp = x1; x1 = x2; x2 = tmp; }
            if (y2 < y1) { int tmp = y1; y1 = y2; y2 = tmp; }

            Imgproc.rectangle(
                rgba,
                new Point(x1, y1),
                new Point(x2, y2),
                color,
                2
            );
        }
    }

    private void DrawBox(Mat rgba, UnityRect r, Scalar color, int thickness = 2)
    {
        if (rgba == null || rgba.empty()) return;

        int imgW = rgba.cols();
        int imgH = rgba.rows();

        float left = (float)r.x;
        float top = (float)r.y;
        float right = (float)(r.x + r.width);
        float bottom = (float)(r.y + r.height);

        int x1 = Mathf.Clamp(Mathf.RoundToInt(left), 0, imgW - 1);
        int y1 = Mathf.Clamp(Mathf.RoundToInt(top), 0, imgH - 1);
        int x2 = Mathf.Clamp(Mathf.RoundToInt(right), 0, imgW - 1);
        int y2 = Mathf.Clamp(Mathf.RoundToInt(bottom), 0, imgH - 1);

        if (x2 < x1) { int tmp = x1; x1 = x2; x2 = tmp; }
        if (y2 < y1) { int tmp = y1; y1 = y2; y2 = tmp; }

        Imgproc.rectangle(rgba, new Point(x1, y1), new Point(x2, y2), color, thickness);
    }

    // Draw paired overlays: invalid faces (black), paired face+hand (same unique color), unmatched hands (black)
    private void DrawTrackingOverlays(Mat rgba, List<FaceTrack> invalidFaces, List<FaceTrack> activeFaces, List<UnityRect> palmBBoxesPx, Dictionary<int, int> faceIdToPalmIndex)
    {
        if (rgba == null || rgba.empty()) return;

        Scalar black = new Scalar(0, 0, 0, 255);

        // 1) invalid faces in black
        DrawBoxes(rgba, invalidFaces, black);

        // 2) find unmatched palms (not mapped to any face)
        HashSet<int> matchedPalms = new HashSet<int>();
        foreach (var kv in faceIdToPalmIndex)
            matchedPalms.Add(kv.Value);

        // 3) draw unmatched palms in black
        for (int i = 0; i < palmBBoxesPx.Count; i++)
        {
            if (!matchedPalms.Contains(i))
                DrawBox(rgba, palmBBoxesPx[i], black);
        }

        // 4) draw active faces and their matched palms in the SAME unique color
        // Build a quick lookup for active face tracks by id
        Dictionary<int, FaceTrack> activeById = new Dictionary<int, FaceTrack>();
        foreach (var f in activeFaces)
            activeById[f.id] = f;

        foreach (var kv in faceIdToPalmIndex)
        {
            int faceId = kv.Key;
            int palmIdx = kv.Value;

            if (!activeById.TryGetValue(faceId, out FaceTrack faceTrack))
                continue;

            if (palmIdx < 0 || palmIdx >= palmBBoxesPx.Count)
                continue;

            Scalar pairColor = ColorForFaceId(faceId);

            // Face + its matched palm use same color
            DrawBox(rgba, faceTrack.bboxPx, pairColor);
            DrawBox(rgba, palmBBoxesPx[palmIdx], pairColor);
        }
    }

    private void EnsureVizBuffers(int w = 1280, int h = 720)
    {
        if (sharedRgba == null || sharedRgba.cols() != w || sharedRgba.rows() != h)
        {
            sharedRgba?.Dispose();
            sharedRgba = new Mat(h, w, CvType.CV_8UC3);
        }

        if (handPoseViz == null || handPoseViz.width != w || handPoseViz.height != h)
        {
            handPoseViz = new Texture2D(w, h, TextureFormat.RGBA32, false);
            handPoseViz.filterMode = FilterMode.Bilinear;
        }
    }

    private List<UnityRect> ToUnityRects(List<drawPredInfo> predInfoList)
    {
        var rects = new List<UnityRect>(predInfoList != null ? predInfoList.Count : 0);
        if (predInfoList == null) return rects;

        for (int i = 0; i < predInfoList.Count; i++)
        {
            var f = predInfoList[i];

            float left = (float)f.left;
            float top = (float)f.top;
            float right = (float)f.right;
            float bottom = (float)f.bottom;

            float xMin = Mathf.Min(left, right);
            float yMin = Mathf.Min(top, bottom);
            float w = Mathf.Abs(right - left);
            float h = Mathf.Abs(bottom - top);

            rects.Add(new UnityRect(xMin, yMin, w, h));
        }

        return rects;
    }

    private void UpdateFaceTracks(List<UnityRect> faceDetectionsPx)
    {
        float now = Time.time;

        // Build list of current track IDs
        List<int> trackIds = new List<int>(_tracks.Keys);

        // Mark tracks as unmatched initially
        HashSet<int> unmatchedTracks = new HashSet<int>(trackIds);
        List<int> unmatchedDetections = new List<int>();
        for (int i = 0; i < faceDetectionsPx.Count; i++) unmatchedDetections.Add(i);

        // Greedy matching: for each detection find nearest track
        foreach (int detIdx in new List<int>(unmatchedDetections))
        {
            UnityRect det = faceDetectionsPx[detIdx];
            Vector2 detCenter = RectCenter(det);

            int bestTrackId = -1;
            float bestDist = float.MaxValue;

            foreach (int tid in unmatchedTracks)
            {
                FaceTrack t = _tracks[tid];
                float d = Vector2.Distance(detCenter, t.centerPx);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestTrackId = tid;
                }
            }

            if (bestTrackId != -1 && bestDist <= faceTrackMaxCenterDistPx)
            {
                // Associate
                FaceTrack t = _tracks[bestTrackId];
                t.bboxPx = det;
                t.centerPx = detCenter;
                t.lastSeenTime = now;

                unmatchedTracks.Remove(bestTrackId);
                unmatchedDetections.Remove(detIdx);
            }
        }

        // Create new tracks for unmatched detections
        foreach (int detIdx in unmatchedDetections)
        {
            UnityRect det = faceDetectionsPx[detIdx];
            FaceTrack t = new FaceTrack
            {
                id = _nextTrackId++,
                bboxPx = det,
                centerPx = RectCenter(det),
                lastSeenTime = now,
                cooldownUntilTime = 0f,
                obfuscated = false,
                lastGesture = HandPoseDetection.Gesture.Unknown,
                sameGestureCount = 0
            };
            _tracks[t.id] = t;
        }

        // Remove stale tracks
        List<int> toRemove = new List<int>();
        foreach (var kv in _tracks)
        {
            FaceTrack t = kv.Value;
            if ((now - t.lastSeenTime) > faceTrackStaleSeconds)
            {
                // Optional: if you want obfuscation to clear on disappearance, do it here.
                // if (t.obfuscated) SetFaceObfuscation(t.id, false, t.bboxPx);
                toRemove.Add(kv.Key);
            }
        }
        foreach (int id in toRemove) _tracks.Remove(id);
    }

    private Dictionary<int, int> MatchPalmsToFaces(List<FaceTrack> activeFaces, List<UnityRect> palmBBoxesPx)
    {
        // For each face, find nearest palm within threshold.
        Dictionary<int, int> faceIdToPalmIndex = new Dictionary<int, int>();

        // We also avoid assigning the same palm to multiple faces.
        HashSet<int> usedPalms = new HashSet<int>();

        foreach (FaceTrack face in activeFaces)
        {
            int bestPalm = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < palmBBoxesPx.Count; i++)
            {
                if (usedPalms.Contains(i)) continue;

                UnityRect palm = palmBBoxesPx[i];
                Vector2 palmCenter = RectCenter(palm);

                float d = Vector2.Distance(palmCenter, face.centerPx);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPalm = i;
                }
            }

            if (bestPalm != -1 && bestDist <= palmToFaceMaxCenterDistPx)
            {
                faceIdToPalmIndex[face.id] = bestPalm;
                usedPalms.Add(bestPalm);
            }
        }

        return faceIdToPalmIndex;
    }

    private bool UpdateGestureStability(FaceTrack t, HandPoseDetection.Gesture g)
    {
        if (t.lastGesture == g)
        {
            t.sameGestureCount++;
        }
        else
        {
            t.lastGesture = g;
            t.sameGestureCount = 1;
        }

        return t.sameGestureCount >= stableGestureFrames;
    }

    private void ResetGestureStability(FaceTrack t, HandPoseDetection.Gesture g)
    {
        if (t.lastGesture != g)
        {
            t.lastGesture = g;
            t.sameGestureCount = 0;
        }
    }

    private void ApplyObfuscationForAllObfuscatedTracks()
    {
        foreach (var kv in _tracks)
        {
            FaceTrack t = kv.Value;
            if (t == null) continue;

            if (t.obfuscated)
            {
                // We blur directly into sharedRgba for this frame
                SetFaceObfuscation(t.id, true, t.bboxPx);
            }
        }
    }

    private void SetFaceObfuscation(int faceTrackId, bool enabled, UnityRect faceBboxPx)
    {
        // Debug.Log($"[Cardea] Face {faceTrackId}: obfuscation={(enabled ? "ON" : "OFF")} bbox={faceBboxPx}");
        // TODO: hook into your obfuscation pipeline:

        int imgW = sharedRgba.cols();
        int imgH = sharedRgba.rows();

        // Convert rect to integer pixel ROI
        float x = faceBboxPx.x;
        float y = faceBboxPx.y;
        float w = faceBboxPx.width;
        float h = faceBboxPx.height;

        // Optional padding to cover hairline/edges
        if (expandBlurBox)
        {
            float padX = w * blurBoxExpandFrac;
            float padY = h * blurBoxExpandFrac;
            x -= padX;
            y -= padY;
            w += 2f * padX;
            h += 2f * padY;
        }

        int x1 = Mathf.Clamp(Mathf.FloorToInt(x), 0, imgW - 1);
        int y1 = Mathf.Clamp(Mathf.FloorToInt(y), 0, imgH - 1);
        int x2 = Mathf.Clamp(Mathf.CeilToInt(x + w), 0, imgW);
        int y2 = Mathf.Clamp(Mathf.CeilToInt(y + h), 0, imgH);

        int roiW = x2 - x1;
        int roiH = y2 - y1;

        if (roiW <= 1 || roiH <= 1)
            return;

        // Kernel size must be odd and > 1
        int k = Mathf.Max(3, faceBlurKernel);
        if ((k % 2) == 0) k += 1;

        // Blur ROI in-place
        using (Mat faceRoi = new Mat(sharedRgba, new OpenCVForUnity.CoreModule.Rect(x1, y1, roiW, roiH)))
        {
            // Use GaussianBlur for nicer anonymization; you can swap to Imgproc.blur for box blur
            Imgproc.GaussianBlur(faceRoi, faceRoi, new Size(k, k), 0);
        }
    }

    private Vector2 RectCenter(UnityRect r)
    {
        return new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
    }

    public Texture2D getCurrCameraFrame()
    {
        return currFrame;
    }

    void OnDestroy()
    {
        sharedRgba?.Dispose();
        sharedRgba = null;
    }

}
