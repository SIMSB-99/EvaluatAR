using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class CSVLogger : MonoBehaviour
{
    // public variables //

    // private variables //
    private string currentDateTime = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
    private string CSVDirPath = Application.dataPath + "/../Logs/";

    private string CSVFilePath;
    private TextWriter tw;
    private string GazeDataCSVFilePath; // this is GazePos
    private TextWriter GazeDatatw;
    private string GazeOriginCSVFilePath;
    private TextWriter GazeOrigintw;
    private string GazeDirCSVFilePath;
    private TextWriter GazeDirtw;
    private string QRDistCSVFilePath;
    private TextWriter QRDisttw;
    private string QRDirCSVFilePath;
    private TextWriter QRDirtw;

    [SerializeField]
    private bool saveGazePosition = false;
    [SerializeField]
    private int FPSCSVWriteFrequency;   // enter -1 to write once only at the end otherwise every x number of lines will be written
    private int writeAtEndNumber = -1;

    private List<string> FPSList = new List<string>();
    private List<Tuple<string, Vector3>> GazePositionList = new List<Tuple<string, Vector3>>(); // this is GazePos
    private List<Tuple<string, Vector3>> GazeOriginList = new List<Tuple<string, Vector3>>();
    private List<Tuple<string, Vector3>> GazeDirList = new List<Tuple<string, Vector3>>();
    private List<Tuple<string, float>> QRDistList = new List<Tuple<string, float>>();
    private List<Tuple<string, Vector3>> QRDirList = new List<Tuple<string, Vector3>>();

    [SerializeField]
    private bool ReadGazeFromCSV = false;
    
    private Queue<Vector3> gazeDataArray = new Queue<Vector3>(); // this is GazePos

    private Queue<Vector3> gazeOriginArray = new Queue<Vector3>();
    private List<Tuple<int, Vector3>> gazeOriginArrayList = new List<Tuple<int, Vector3>>();

    private Queue<Vector3> gazeDirArray = new Queue<Vector3>();
    private List<Tuple<int, Vector3>> gazeDirArrayList = new List<Tuple<int, Vector3>>();

    private Queue<float> QRDistArray = new Queue<float>();
    private Queue<Vector3> QRDirArray = new Queue<Vector3>();

    private List<float> gazeOriginXArray = new List<float>();
    private List<float> gazeOriginYArray = new List<float>();
    private List<float> gazeOriginZArray = new List<float>();

    private float gazeOriginMinX;
    private float gazeOriginMaxX;

    private float gazeOriginMinY;
    private float gazeOriginMaxY;

    private float gazeOriginMinZ;
    private float gazeOriginMaxZ;

    private List<float> QRDistXArray = new List<float>();

    private float QRDistMin;
    private float QRDistMax;

    private List<float> QRDirXArray = new List<float>();
    private List<float> QRDirYArray = new List<float>();
    private List<float> QRDirZArray = new List<float>();

    private float QRDirMinX;
    private float QRDirMaxX;

    private float QRDirMinY;
    private float QRDirMaxY;

    private float QRDirMinZ;
    private float QRDirMaxZ;

    // functions //

    void Awake()
    {
        // checking for csv directory 
        if (!Directory.Exists(CSVDirPath))
        {
            Directory.CreateDirectory(CSVDirPath);
        }

        // create new csv filepath
        CSVFilePath = Application.dataPath + "/../Logs/" + currentDateTime + ".csv";

        if (saveGazePosition)
        {
            // Gaze Position
            GazeDataCSVFilePath = Application.dataPath + "/../Logs/GazePos" + currentDateTime + ".csv";
            GazeDatatw = new StreamWriter(GazeDataCSVFilePath, false); // false indicates overwriting the file
            GazeDatatw.WriteLine("TimeStamp, Position-x, Position-y, Position-z"); // edit this to update coloumns of CSV
            GazeDatatw.Close();

            // Gaze Origin
            GazeOriginCSVFilePath = Application.dataPath + "/../Logs/GazeOrigin" + currentDateTime + ".csv";
            GazeOrigintw = new StreamWriter(GazeOriginCSVFilePath, false);
            GazeOrigintw.WriteLine("TimeStamp, Position-x, Position-y, Position-z");
            GazeOrigintw.Close();

            // Gaze Dir
            GazeDirCSVFilePath = Application.dataPath + "/../Logs/GazeDir" + currentDateTime + ".csv";
            GazeDirtw = new StreamWriter(GazeDirCSVFilePath, false);
            GazeDirtw.WriteLine("TimeStamp, Position-x, Position-y, Position-z");
            GazeDirtw.Close();

            // QR Dist
            QRDistCSVFilePath = Application.dataPath + "/../Logs/QRDist" + currentDateTime + ".csv";
            QRDisttw = new StreamWriter(QRDistCSVFilePath, false);
            QRDisttw.WriteLine("TimeStamp, distance");
            QRDisttw.Close();

            // QR Dir
            QRDirCSVFilePath = Application.dataPath + "/../Logs/QRDir" + currentDateTime + ".csv";
            QRDirtw = new StreamWriter(QRDirCSVFilePath, false);
            QRDirtw.WriteLine("TimeStamp, Position-x, Position-y, Position-z");
            QRDirtw.Close();
        }

        // Performance metrics logger
        tw = new StreamWriter(CSVFilePath, false);
        tw.WriteLine("TimeStamp, FPS, # Faces, frame #, QRDetection Status, Hardcoded Eye Gaze, DetectedFaces Info");
        tw.Close();
    }

    void Update()
    {
    }

    public void addFPStoList(string fpsLine)
    {
        if (FPSCSVWriteFrequency == writeAtEndNumber)
        {
            FPSList.Add(fpsLine);
        }

        if (FPSCSVWriteFrequency == 1)
        {
            Debug.Log("Writing every line directly without lists");

            tw = new StreamWriter(CSVFilePath, true); // true to indicate no overwriting, just append
            tw.WriteLine(fpsLine);
            tw.Close();
        }
        else
        {
            if (FPSList.Count >= FPSCSVWriteFrequency)
            {
                batchWriteToFPSFileOnly();

                FPSList = new List<string>();
            }

            FPSList.Add(fpsLine);
        }
    }

    public void addGazePositiontoList(string timestamp, Vector3 gazePositionLine)
    {
        var timeAndGazePositionTupel = new Tuple<string, Vector3>(timestamp, gazePositionLine);
        GazePositionList.Add(timeAndGazePositionTupel);
    }

    public void addGazeOriginToList(string timestamp, Vector3 gazeOriginLine)
    {
        var timeAndGazeOriginTupel = new Tuple<string, Vector3>(timestamp, gazeOriginLine);
        GazeOriginList.Add(timeAndGazeOriginTupel);
    }

    public void addGazeDirToList(string timestamp, Vector3 gazeDirLine)
    {
        var timeAndGazeDirTupel = new Tuple<string, Vector3>(timestamp, gazeDirLine);
        GazeDirList.Add(timeAndGazeDirTupel);
    }

    public void addQRToCameraDistanceToList(string timestamp, float QRdist)
    {
        var timeAndQRDirTupel = new Tuple<string, float>(timestamp, QRdist);
        QRDistList.Add(timeAndQRDirTupel);
    }

    public void addQRToCameraDirectionToList(string timestamp, Vector3 QRdir)
    {
        var timeAndQRDirTupel = new Tuple<string, Vector3>(timestamp, QRdir);
        QRDirList.Add(timeAndQRDirTupel);
    }

    void OnApplicationPause()
    {
        batchWriteToFiles();

        FPSList = new List<string>();
        GazePositionList = new List<Tuple<string, Vector3>>();
        GazeOriginList = new List<Tuple<string, Vector3>>();
        GazeDirList = new List<Tuple<string, Vector3>>();
        QRDistList = new List<Tuple<string, float>>();
        QRDirList = new List<Tuple<string, Vector3>>();
    }

    public void batchWriteToFPSFileOnly()
    {
        tw = new StreamWriter(CSVFilePath, true); // true to indicate no overwriting, just append
        for (int i = 0; i < FPSList.Count; i++)
        {
            tw.WriteLine(FPSList[i]);
        }

        tw.Close();
    }

    public void batchWriteToFiles()
    {
        // Performance metrics logger
        batchWriteToFPSFileOnly();

        /*
        // Performance metrics logger
        tw = new StreamWriter(CSVFilePath, true); // true to indicate no overwriting, just append
        for (int i = 0; i < FPSList.Count; i++)
        {
            tw.WriteLine(FPSList[i]);
        }

        tw.Close();
        */

        // check if eye tracking data is being recorded, if yes then store in relevant files 
        if (saveGazePosition)
        {
            // Gaze Pos
            GazeDatatw = new StreamWriter(GazeDataCSVFilePath, true); // true to indicate no overwriting, just append
            for (int i = 0; i < GazePositionList.Count; i++)
            {
                var tempLine = GazePositionList[i].Item1 + "," + GazePositionList[i].Item2.ToString();
                GazeDatatw.WriteLine(tempLine);
            }

            GazeDatatw.Close();

            // Gaze Origin
            GazeOrigintw = new StreamWriter(GazeOriginCSVFilePath, true);
            for (int i = 0; i < GazeOriginList.Count; i++)
            {
                var tempLine = GazeOriginList[i].Item1 + "," + GazeOriginList[i].Item2.ToString();
                GazeOrigintw.WriteLine(tempLine);
            }

            GazeOrigintw.Close();

            // Gaze Dir
            GazeDirtw = new StreamWriter(GazeDirCSVFilePath, true);
            for (int i = 0; i < GazeDirList.Count; i++)
            {
                var tempLine = GazeDirList[i].Item1 + "," + GazeDirList[i].Item2.ToString();
                GazeDirtw.WriteLine(tempLine);
            }

            GazeDirtw.Close();

            // QR Dist
            QRDisttw = new StreamWriter(QRDistCSVFilePath, true);
            for (int i = 0; i < QRDistList.Count; i++)
            {
                var tempLine = QRDistList[i].Item1 + "," + QRDistList[i].Item2.ToString();
                QRDisttw.WriteLine(tempLine);
            }

            QRDisttw.Close();

            // QR Dir
            QRDirtw = new StreamWriter(QRDirCSVFilePath, true);
            for (int i = 0; i < QRDirList.Count; i++)
            {
                var tempLine = QRDirList[i].Item1 + "," + QRDirList[i].Item2.ToString();
                QRDirtw.WriteLine(tempLine);
            }

            QRDirtw.Close();
        }
    }

    public bool getSaveGazePosition()
    {
        return saveGazePosition; 
    }

    public bool getReadGazeFromCSV()
    {
        return ReadGazeFromCSV;
    }

    public Queue<Vector3> loadGazeDataCSV(string GazeDataFile)
    {  
        if (File.Exists(GazeDataFile))
        {

            StreamReader strReader = new StreamReader(GazeDataFile);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                var data_values = line.Split(',');

                // remove the leading ( and trailing ) from the line
                line = data_values[1] + "," + data_values[2] + "," + data_values[3];
                line = line.Substring(1);
                line = line.Remove(line.Length - 1);

                // Debug.Log("Final read line: " + line);

                data_values = line.Split(',');

                Vector3 GazeVector = new Vector3(float.Parse(data_values[0]), float.Parse(data_values[1]), float.Parse(data_values[2]));
                // Debug.Log("GazeVector: " + GazeVector);

                gazeDataArray.Enqueue(GazeVector);
            }
        }

        return gazeDataArray;
    }

    public Queue<Vector3> loadGazeOriginDataCSV(string GazeOriginFile)
    {
        if (File.Exists(GazeOriginFile))
        {

            StreamReader strReader = new StreamReader(GazeOriginFile);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                var data_values = line.Split(',');

                // remove the leading ( and trailing ) from the line
                line = data_values[1] + "," + data_values[2] + "," + data_values[3];
                line = line.Substring(1);
                line = line.Remove(line.Length - 1);

                data_values = line.Split(',');

                Vector3 GazeOriginVector = new Vector3(float.Parse(data_values[0]), float.Parse(data_values[1]), float.Parse(data_values[2]));

                gazeOriginArray.Enqueue(GazeOriginVector);

                gazeOriginXArray.Add(GazeOriginVector.x);
                gazeOriginYArray.Add(GazeOriginVector.y);
                gazeOriginZArray.Add(GazeOriginVector.z);
            }
        }

        var gazeOriginXActualArray = gazeOriginXArray.ToArray();
        var gazeOriginYActualArray = gazeOriginYArray.ToArray();
        var gazeOriginZActualArray = gazeOriginZArray.ToArray();

        gazeOriginMinX = gazeOriginXActualArray.Min();
        gazeOriginMaxX = gazeOriginXActualArray.Max();

        gazeOriginMinY = gazeOriginYActualArray.Min();
        gazeOriginMaxY = gazeOriginYActualArray.Max();

        gazeOriginMinZ = gazeOriginZActualArray.Min();
        gazeOriginMaxZ = gazeOriginZActualArray.Max();

        return gazeOriginArray;
    }

    public List<Tuple<int, Vector3>> loadListFromGazeOriginDataCSV(string GazeOriginFile)
    {
        if (File.Exists(GazeOriginFile))
        {

            StreamReader strReader = new StreamReader(GazeOriginFile);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                var data_values = line.Split(',');

                // get time stamp
                int elapsedTime = Int32.Parse(data_values[0]);

                // remove the leading ( and trailing ) from the line
                line = data_values[1] + "," + data_values[2] + "," + data_values[3];
                line = line.Substring(1);
                line = line.Remove(line.Length - 1);

                data_values = line.Split(',');

                Vector3 GazeOriginVector = new Vector3(float.Parse(data_values[0]), float.Parse(data_values[1]), float.Parse(data_values[2]));

                // gazeOriginArray.Enqueue(GazeOriginVector);
                var timeAndGazeOriginTupel = new Tuple<int, Vector3>(elapsedTime, GazeOriginVector);
                gazeOriginArrayList.Add(timeAndGazeOriginTupel);

                gazeOriginXArray.Add(GazeOriginVector.x);
                gazeOriginYArray.Add(GazeOriginVector.y);
                gazeOriginZArray.Add(GazeOriginVector.z);
            }
        }

        var gazeOriginXActualArray = gazeOriginXArray.ToArray();
        var gazeOriginYActualArray = gazeOriginYArray.ToArray();
        var gazeOriginZActualArray = gazeOriginZArray.ToArray();

        gazeOriginMinX = gazeOriginXActualArray.Min();
        gazeOriginMaxX = gazeOriginXActualArray.Max();

        gazeOriginMinY = gazeOriginYActualArray.Min();
        gazeOriginMaxY = gazeOriginYActualArray.Max();

        gazeOriginMinZ = gazeOriginZActualArray.Min();
        gazeOriginMaxZ = gazeOriginZActualArray.Max();

        return gazeOriginArrayList;
    }

    public float getgazeOriginMinX()
    {
        return gazeOriginMinX;
    }

    public float getgazeOriginMaxX()
    {
        return gazeOriginMaxX;
    }

    public float getgazeOriginMinY()
    {
        return gazeOriginMinY;
    }

    public float getgazeOriginMaxY()
    {
        return gazeOriginMaxY;
    }

    public float getgazeOriginMinZ()
    {
        return gazeOriginMinZ;
    }

    public float getgazeOriginMaxZ()
    {
        return gazeOriginMaxZ;
    }

    public Queue<Vector3> loadGazeDirDataCSV(string GazeDirFile)
    {
        if (File.Exists(GazeDirFile))
        {

            StreamReader strReader = new StreamReader(GazeDirFile);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                var data_values = line.Split(',');

                // remove the leading ( and trailing ) from the line
                line = data_values[1] + "," + data_values[2] + "," + data_values[3];
                line = line.Substring(1);
                line = line.Remove(line.Length - 1);

                data_values = line.Split(',');

                Vector3 GazeDirVector = new Vector3(float.Parse(data_values[0]), float.Parse(data_values[1]), float.Parse(data_values[2]));

                gazeDirArray.Enqueue(GazeDirVector);
            }
        }

        return gazeDirArray;
    }

    public List<Tuple<int, Vector3>> loadListFromGazeDirDataCSV(string GazeDirFile)
    {
        if (File.Exists(GazeDirFile))
        {

            StreamReader strReader = new StreamReader(GazeDirFile);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                var data_values = line.Split(',');

                // get time stamp
                int elapsedTime = Int32.Parse(data_values[0]);

                // remove the leading ( and trailing ) from the line
                line = data_values[1] + "," + data_values[2] + "," + data_values[3];
                line = line.Substring(1);
                line = line.Remove(line.Length - 1);

                data_values = line.Split(',');

                Vector3 GazeDirVector = new Vector3(float.Parse(data_values[0]), float.Parse(data_values[1]), float.Parse(data_values[2]));

                // gazeDirArray.Enqueue(GazeDirVector);
                var timeAndGazeOriginTupel = new Tuple<int, Vector3>(elapsedTime, GazeDirVector);
                gazeDirArrayList.Add(timeAndGazeOriginTupel);
            }
        }

        return gazeDirArrayList;
    }

    public Queue<float> loadQRDistDataCSV(string QRDistFile)
    {
        if (File.Exists(QRDistFile))
        {

            StreamReader strReader = new StreamReader(QRDistFile);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                var data_values = line.Split(',');

                float QRDistFloat = float.Parse(data_values[1]);

                QRDistArray.Enqueue(QRDistFloat);
            }
        }

        var QRDistActualArray = QRDistArray.ToArray();

        QRDistMin = QRDistActualArray.Min();
        QRDistMax = QRDistActualArray.Max();

        return QRDistArray;
    }

    public float getQRDistMin()
    {
        return QRDistMin;
    }

    public float getQRDistMax()
    {
        return QRDistMax;
    }

    public Queue<Vector3> loadQRDirDataCSV(string QRDirFile)
    {
        if (File.Exists(QRDirFile))
        {

            StreamReader strReader = new StreamReader(QRDirFile);
            bool eof = false;

            while (!eof)
            {
                string line = strReader.ReadLine();

                if (line == null)
                {
                    eof = true;
                    break;
                }

                var data_values = line.Split(',');

                // remove the leading ( and trailing ) from the line
                line = data_values[1] + "," + data_values[2] + "," + data_values[3];
                line = line.Substring(1);
                line = line.Remove(line.Length - 1);

                data_values = line.Split(',');

                Vector3 QRDirVector = new Vector3(float.Parse(data_values[0]), float.Parse(data_values[1]), float.Parse(data_values[2]));

                QRDirArray.Enqueue(QRDirVector);

                QRDirXArray.Add(QRDirVector.x);
                QRDirYArray.Add(QRDirVector.y);
                QRDirZArray.Add(QRDirVector.z);
            }
        }

        var QRDirXActualArray = QRDirXArray.ToArray();
        var QRDirYActualArray = QRDirYArray.ToArray();
        var QRDirZActualArray = QRDirZArray.ToArray();

        QRDirMinX = QRDirXActualArray.Min();
        QRDirMaxX = QRDirXActualArray.Max();

        QRDirMinY = QRDirYActualArray.Min();
        QRDirMaxY = QRDirYActualArray.Max();

        QRDirMinZ = QRDirZActualArray.Min();
        QRDirMaxZ = QRDirZActualArray.Max();

        return QRDirArray;
    }

    public float getQRDirMinX()
    {
        return QRDirMinX;
    }

    public float getQRDirMaxX()
    {
        return QRDirMaxX;
    }

    public float getQRDirMinY()
    {
        return QRDirMinY;
    }

    public float getQRDirMaxY()
    {
        return QRDirMaxY;
    }

    public float getQRDirMinZ()
    {
        return QRDirMinZ;
    }

    public float getQRDirMaxZ()
    {
        return QRDirMaxZ;
    }
}
