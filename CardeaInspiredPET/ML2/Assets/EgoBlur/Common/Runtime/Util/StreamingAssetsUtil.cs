using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace EgoBlur
{
    public static class StreamingAssetsUtil
    {
        public static IEnumerator EnsureStreamingAssetOnDisk(string relativePathFromStreamingAssets, Action<string> onReady)
        {
            //string src = Path.Combine(Application.streamingAssetsPath, relativePathFromStreamingAssets);
            //string dst = Path.Combine(Application.persistentDataPath, relativePathFromStreamingAssets);
            string logsPath = Application.dataPath + "/../Logs";
            string dst = Path.Combine(logsPath, relativePathFromStreamingAssets);

            //Directory.CreateDirectory(Path.GetDirectoryName(dst));

            // If already copied, reuse
            if (File.Exists(dst))
            {
                onReady(dst);
                yield break;
            }
            //else
            //{
            //    throw new Exception($"Failed to load model files");
            //}


            //try
            //{
            //    if (File.Exists(dst))
            //    {
            //        onReady(dst);
            //        yield break;
            //    }
            //}
            //catch (Exception e)
            //{
            //    throw new Exception($"Failed to load model files. error: {e}");
            //}


            //// Android/UWP: StreamingAssets is not a normal file path, use UnityWebRequest
            //using (var req = UnityWebRequest.Get(src))
            //{
            //    yield return req.SendWebRequest();
            //    if (req.result != UnityWebRequest.Result.Success)
            //    {
            //        Debug.Log("StreamingAssetsUtil // writing to assets ");
            //        throw new Exception($"Failed to load StreamingAsset: {src}\n{req.error}");
            //    }


            //    File.WriteAllBytes(dst, req.downloadHandler.data);
            //}


            onReady(dst);
        }
    }
}
