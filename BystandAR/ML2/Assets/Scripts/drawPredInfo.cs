using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class drawPredInfo : MonoBehaviour
{
    public int classId;
    public float conf;
    public double left;
    public double top;
    public double right;
    public double bottom;

    public drawPredInfo()
    {
        this.classId = 0;
        this.conf = 0;
        this.left = 0;
        this.top = 0;
        this.right = 0;
        this.bottom = 0;
    }

    public drawPredInfo(int cid, float con, double l, double t, double r, double b)
    {
        this.classId = cid;
        this.conf = con;
        this.left = l;
        this.top = t;
        this.right = r;
        this.bottom = b;
    }
}
