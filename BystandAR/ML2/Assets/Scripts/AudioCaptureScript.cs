using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR.MagicLeap;

public class AudioCaptureScript : MonoBehaviour
{
    // public variables //

    // private variables //
    // [SerializeField, Tooltip("The audio source that should replay the captured audio.")]
    private AudioSource _playbackAudioSource = null;
    // [Header("Delayed Playback")]
    // [SerializeField, Range(1, 2), Tooltip("The pitch used for delayed audio playback.")]
    private float _pitch = 1.5f;

    private bool isAudioDetected = false;
    private float audioLastDetectionTime = 0;
    private float audioDetectionStart = 0;

    private const int AUDIO_CLIP_LENGTH_SECONDS = 60;
    [SerializeField, Range(0, 1)]
    private float AudioSensitivity = 0.02f;
    private const float AUDIO_CLIP_TIMEOUT_SECONDS = 2;

    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();
    private MLAudioInput.BufferClip mlAudioBufferClip;

    private bool isTalking = false;

    // functions //

    void Awake()
    {
        // Debug.Log("AudioCaptureScript // In Awake");

        if (_playbackAudioSource == null)
        {
            // Debug.Log("PlaybackAudioSource is not set, adding component to " + gameObject.name);
            _playbackAudioSource = gameObject.AddComponent<AudioSource>();
        }
        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        MLPermissions.RequestPermission(MLPermission.RecordAudio, permissionCallbacks);
    }

    void OnDestroy()
    {
        // Debug.Log("AudioCaptureScript // In OnDestroy");

        StopCapture();
        permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
    }

    private void StartMicrophone()
    {
        // Debug.Log("AudioCaptureScript // In StartMicrophone");

        _playbackAudioSource.Stop();
        var captureType = MLAudioInput.MicCaptureType.VoiceCapture;
        mlAudioBufferClip = new MLAudioInput.BufferClip(MLAudioInput.MicCaptureType.VoiceCapture, AUDIO_CLIP_LENGTH_SECONDS, MLAudioInput.GetSampleRate(captureType));
        mlAudioBufferClip.OnReceivedSamples += DetectAudio;
        _playbackAudioSource.pitch = _pitch;
        _playbackAudioSource.clip = null;
        _playbackAudioSource.loop = false;
        isAudioDetected = false;
        audioDetectionStart = 0;
        audioLastDetectionTime = 0;
    }

    private void StopCapture()
    {
        // Debug.Log("AudioCaptureScript // In StopCapture");

        mlAudioBufferClip?.Dispose();
        mlAudioBufferClip = null;

        // Stop audio playback source and reset settings.
        _playbackAudioSource.Stop();
        _playbackAudioSource.time = 0;
        _playbackAudioSource.pitch = 1;
        _playbackAudioSource.loop = false;
        _playbackAudioSource.clip = null;
    }

    private void OnPermissionGranted(string permission)
    {
        // Debug.Log("AudioCaptureScript // In OnPermissionGranted");

        StartMicrophone();
    }

    private void DetectAudio(float[] samples)
    {
        // Debug.Log("AudioCaptureScript // In DetectAudio");

        // Analyze the input spectrum data, to determine when someone is speaking.
        float maxAudioSample = 0f;

        maxAudioSample = samples.Append(maxAudioSample).Max();
        if (maxAudioSample > AudioSensitivity)
        {
            // Debug.Log("AudioCaptureScript // In DetectAudio // maxAudioSample > AudioSensitivity");
            isTalking = true;

            /*
            audioLastDetectionTime = Time.time;
            if (isAudioDetected == false)
            {
                isAudioDetected = true;
                audioDetectionStart = Time.time;
            }
            */
        }
        else
        {
            isTalking = false;
        }
        /*
        else if (isAudioDetected && (Time.time > audioLastDetectionTime + AUDIO_CLIP_TIMEOUT_SECONDS))
        {
            // Debug.Log("AudioCaptureScript // In DetectAudio // else if -> play sound");

            var audioDetectionDuration = Time.time - audioDetectionStart;

            _playbackAudioSource.clip = mlAudioBufferClip.FlushToClip();
            _playbackAudioSource.time = _playbackAudioSource.clip.length - audioDetectionDuration;
            _playbackAudioSource.Play();
            // Reset and allow for new captured speech.
            isAudioDetected = false;
            audioDetectionStart = 0;
            audioLastDetectionTime = 0;
        }

        // Debug.Log("AudioCaptureScript // In DetectAudio // outside if else");
        */
    }

    public bool getIsTalking()
    {
        // Debug.Log("EyeTracking // In getIsTalking");

        return isTalking;
    }


}
