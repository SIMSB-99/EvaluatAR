using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using UnityEngine;

namespace EgoBlur
{
    public sealed class EgoBlurOnnxRuntimeMultiBackend : IEgoBlurMultiBackend
    {
        private InferenceSession _face, _lp;
        private string _faceInput, _lpInput;

        public void Load(string faceModelPathOnDisk, string lpModelPathOnDisk)
        {
            Dispose();

            Debug.Log("face model path: " + faceModelPathOnDisk);
            Debug.Log("lp model path: " + lpModelPathOnDisk);

            var opts = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
            _face = new InferenceSession(faceModelPathOnDisk, opts);
            _lp = new InferenceSession(lpModelPathOnDisk, opts);

            Debug.Log("both inference session created");

            _faceInput = FirstKey(_face.InputMetadata);
            _lpInput = FirstKey(_lp.InputMetadata);

            Debug.Log("first keys set!");
        }

        public (EgoBlurResult face, EgoBlurResult lp) RunBoth(float[] inputCHW, float scoreThreshold)
        {
            Debug.Log("about to run both models");

            var inputTensor = new DenseTensor<float>(inputCHW, new int[] { 3, EgoBlurPreprocessorExact.H, EgoBlurPreprocessorExact.W });

            var faceRes = RunOne(_face, _faceInput, inputTensor, scoreThreshold);
            Debug.Log("ran face model");
            var lpRes = RunOne(_lp, _lpInput, inputTensor, scoreThreshold);

            Debug.Log("ran both models, about to return results");
            return (faceRes, lpRes);
        }

        private static EgoBlurResult RunOne(InferenceSession sess, string inputName, DenseTensor<float> inputTensor, float thr)
        {
            Debug.Log("creating tensors for inputs");

            var inputs = new List<NamedOnnxValue>(1)
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            Debug.Log("I am about to call the model with inputs!");

            using var outputs = sess.Run(inputs);

            Debug.Log("right after calling the model");

            var boxesT = Get(outputs, "boxes").AsTensor<float>();
            var labelsT = Get(outputs, "labels").AsTensor<long>();
            var scoresT = Get(outputs, "scores").AsTensor<float>();
            var sizeT = Get(outputs, "image_size").AsTensor<long>();

            var res = new EgoBlurResult
            {
                imgH = checked((int)sizeT[0]),
                imgW = checked((int)sizeT[1])
            };

            int n = checked((int)scoresT.Length);
            for (int i = 0; i < n; i++)
            {
                float s = scoresT[i];
                if (s < thr) continue;

                float x1 = boxesT[i, 0];
                float y1 = boxesT[i, 1];
                float x2 = boxesT[i, 2];
                float y2 = boxesT[i, 3];

                res.dets.Add(new EgoBlurDet
                {
                    score = s,
                    label = labelsT[i],
                    rect = new UnityEngine.Rect(x1, y1, x2 - x1, y2 - y1)
                });
            }
            return res;
        }

        private static DisposableNamedOnnxValue Get(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> col, string name)
        {
            foreach (var v in col) if (v.Name == name) return v;
            throw new Exception($"Output '{name}' not found.");
        }

        private static string FirstKey(IReadOnlyDictionary<string, NodeMetadata> dict)
        {
            foreach (var kv in dict) return kv.Key;
            throw new Exception("No inputs found.");
        }

        public void Dispose()
        {
            _face?.Dispose(); _face = null;
            _lp?.Dispose(); _lp = null;
        }
    }
}
