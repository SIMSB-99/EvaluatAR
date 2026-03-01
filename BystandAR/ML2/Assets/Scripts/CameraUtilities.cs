using UnityEngine;
using UnityEngine.XR.MagicLeap;

public class CameraUtilities
{
    // The length to be used for the ray when no intersection with the WorldMesh is found.
    // This prevents the ray from "popping" or suddenly changing length visually.
    private static float _rayLength = 3;

    /// <summary>
    /// Casts a ray from a 2D screen pixel position to a point in world space.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="cameraTransformMatrix">Transform matrix of the camera.</param>
    /// <param name="screenPoint">2D screen point to be cast.</param>
    /// <returns>The world space position where the ray intersects with the WorldMesh.</returns>
    // public static Vector3 CastRayFromScreenToWorldPoint(MLCamera.IntrinsicCalibrationParameters icp, Matrix4x4 cameraTransformMatrix, Vector2 screenPoint)
    public static Vector3 CastRayFromScreenToWorldPoint(uint currIcpWidth, uint currIcpHeight, Vector2 currIcpFocalLength, Vector2 currIcpPrincipalPoint, Matrix4x4 cameraTransformMatrix, Vector2 screenPoint, float currIcpDistortion0, float currIcpDistortion1, float currIcpDistortion2, float currIcpDistortion3, float currIcpDistortion4)
    {
        /*
        var width = icp.Width;
        var height = icp.Height;
        */
        var width = currIcpWidth;
        var height = currIcpHeight;

        // Convert pixel coordinates to normalized viewport coordinates.
        var viewportPoint = new Vector2(screenPoint.x / width, screenPoint.y / height);

        // return CastRayFromViewPortToWorldPoint(icp, cameraTransformMatrix, viewportPoint);
        return CastRayFromViewPortToWorldPoint(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, cameraTransformMatrix, viewportPoint, currIcpDistortion0, currIcpDistortion1, currIcpDistortion2, currIcpDistortion3, currIcpDistortion4);
    }

    /// <summary>
    /// Casts a ray from a 2D viewport position to a point in world space.
    /// This method is used as Unity's Camera.ScreenToWorld functions are limited to Unity's virtual cameras,
    /// whereas this method provides a raycast from the actual physical RGB camera.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="cameraTransformMatrix">Transform matrix of the camera.</param>
    /// <param name="viewportPoint">2D viewport point to be cast.</param>
    /// <returns>The world space position where the ray intersects with the WorldMesh.</returns>
    // public static Vector3 CastRayFromViewPortToWorldPoint(MLCamera.IntrinsicCalibrationParameters icp, Matrix4x4 cameraTransformMatrix, Vector2 viewportPoint)
    public static Vector3 CastRayFromViewPortToWorldPoint(uint currIcpWidth, uint currIcpHeight, Vector2 currIcpFocalLength, Vector2 currIcpPrincipalPoint, Matrix4x4 cameraTransformMatrix, Vector2 viewportPoint, float currIcpDistortion0, float currIcpDistortion1, float currIcpDistortion2, float currIcpDistortion3, float currIcpDistortion4)
    {
        // Undistort the viewport point to account for lens distortion.
        // var undistortedViewportPoint = UndistortViewportPoint(icp, viewportPoint);
        var undistortedViewportPoint = UndistortViewportPoint(currIcpWidth, currIcpHeight, currIcpPrincipalPoint, viewportPoint, currIcpDistortion0, currIcpDistortion1, currIcpDistortion2, currIcpDistortion3, currIcpDistortion4);

        // Create a ray based on the undistorted viewport point that projects out of the RGB camera.
        // Ray ray = RayFromViewportPoint(icp, undistortedViewportPoint, cameraTransformMatrix.GetPosition(), cameraTransformMatrix.rotation);
        Ray ray = RayFromViewportPoint(currIcpWidth, currIcpHeight, currIcpFocalLength, currIcpPrincipalPoint, undistortedViewportPoint, cameraTransformMatrix.GetPosition(), cameraTransformMatrix.rotation);

        // By default, set the hit point at a fixed length away.
        Vector3 hitPoint = ray.GetPoint(_rayLength);

        // Raycast against the WorldMesh to find where the ray intersects.

        // we need collision with all other layers other than face cubes
        LayerMask mask = LayerMask.GetMask("Face Cubes");
        mask = ~mask;

        if (Physics.Raycast(ray, out RaycastHit hit, 100, mask))
        {
            hitPoint = hit.point;
            _rayLength = hit.distance;
        }

        return hitPoint;
    }

    /// <summary>
    /// Undistorts a viewport point to account for lens distortion.
    /// https://en.wikipedia.org/wiki/Distortion_(optics)
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="distortedViewportPoint">The viewport point that may have distortion.</param>
    /// <returns>The corrected/undistorted viewport point.</returns>
    // public static Vector2 UndistortViewportPoint(MLCamera.IntrinsicCalibrationParameters icp, Vector2 distortedViewportPoint)
    public static Vector2 UndistortViewportPoint(uint currIcpWidth, uint currIcpHeight, Vector2 currIcpPrincipalPoint, Vector2 distortedViewportPoint, float currIcpDistortion0, float currIcpDistortion1, float currIcpDistortion2, float currIcpDistortion3, float currIcpDistortion4)
    {
        // var normalizedToPixel = new Vector2(icp.Width / 2, icp.Height / 2).magnitude;
        var normalizedToPixel = new Vector2(currIcpWidth / 2, currIcpHeight / 2).magnitude;
        var pixelToNormalized = Mathf.Approximately(normalizedToPixel, 0) ? float.MaxValue : 1 / normalizedToPixel;
        // var viewportToNormalized = new Vector2(icp.Width * pixelToNormalized, icp.Height * pixelToNormalized);
        var viewportToNormalized = new Vector2(currIcpWidth * pixelToNormalized, currIcpHeight * pixelToNormalized);
        // var normalizedPrincipalPoint = icp.PrincipalPoint * pixelToNormalized;
        var normalizedPrincipalPoint = currIcpPrincipalPoint * pixelToNormalized;
        var normalizedToViewport = new Vector2(1 / viewportToNormalized.x, 1 / viewportToNormalized.y);

        Vector2 d = Vector2.Scale(distortedViewportPoint, viewportToNormalized);
        Vector2 o = d - normalizedPrincipalPoint;

        // Distortion coefficients.
        /*
        float K1 = (float)icp.Distortion[0];
        float K2 = (float)icp.Distortion[1];
        float P1 = (float)icp.Distortion[2];
        float P2 = (float)icp.Distortion[3];
        float K3 = (float)icp.Distortion[4];
        */
        float K1 = currIcpDistortion0;
        float K2 = currIcpDistortion1;
        float P1 = currIcpDistortion2;
        float P2 = currIcpDistortion3;
        float K3 = currIcpDistortion4;

        float r2 = o.sqrMagnitude;
        float r4 = r2 * r2;
        float r6 = r2 * r4;

        float radial = K1 * r2 + K2 * r4 + K3 * r6;
        Vector3 u = d + o * radial;

        // Tangential distortion correction.
        if (!Mathf.Approximately(P1, 0) || !Mathf.Approximately(P2, 0))
        {
            u.x += P1 * (r2 + 2 * o.x * o.x) + 2 * P2 * o.x * o.y;
            u.y += P2 * (r2 + 2 * o.y * o.y) + 2 * P1 * o.x * o.y;
        }

        return Vector2.Scale(u, normalizedToViewport);
    }

    /// <summary>
    /// Creates a ray projecting out from the RGB camera based on a viewport point.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="viewportPoint">2D viewport point to create the ray from.</param>
    /// <param name="cameraPos">Position of the camera.</param>
    /// <param name="cameraRotation">Rotation of the camera.</param>
    /// <returns>The created ray based on the viewport point.</returns>
    // public static Ray RayFromViewportPoint(MLCamera.IntrinsicCalibrationParameters icp, Vector2 viewportPoint, Vector3 cameraPos, Quaternion cameraRotation)
    public static Ray RayFromViewportPoint(uint currIcpWidth, uint currIcpHeight, Vector2 currIcpFocalLength, Vector2 currIcpPrincipalPoint, Vector2 viewportPoint, Vector3 cameraPos, Quaternion cameraRotation)
    {
        /*
        var width = icp.Width;
        var height = icp.Height;
        var principalPoint = icp.PrincipalPoint;
        var focalLength = icp.FocalLength;
        */

        var width = currIcpWidth;
        var height = currIcpHeight;
        var principalPoint = currIcpPrincipalPoint;
        var focalLength = currIcpFocalLength;

        Vector2 pixelPoint = new Vector2(viewportPoint.x * width, viewportPoint.y * height);
        Vector2 offsetPoint = new Vector2(pixelPoint.x - principalPoint.x, pixelPoint.y - (height - principalPoint.y));
        Vector2 unitFocalLength = new Vector2(offsetPoint.x / focalLength.x, offsetPoint.y / focalLength.y);

        Vector3 rayDirection = cameraRotation * new Vector3(unitFocalLength.x, unitFocalLength.y, 1).normalized;

        return new Ray(cameraPos, rayDirection);
    }



    /// <summary>
    /// Converts a 3D world position into a 2D screen pixel coordinate.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="cameraTransformMatrix">Transform matrix of the camera.</param>
    /// <param name="worldPoint">3D world point to be converted.</param>
    /// <returns>The screen pixel coordinates corresponding to the given world space position.</returns>
    // public static Vector2 ConvertWorldPointToScreen(MLCamera.IntrinsicCalibrationParameters icp, Matrix4x4 cameraTransformMatrix, Vector3 worldPoint)
    public static Vector2 ConvertWorldPointToScreen(uint icpWidth, uint icpHeight, Vector2 icpFocalLength, Vector2 icpPrincipalPoint, Matrix4x4 cameraTransformMatrix, Vector3 worldPoint)
    {
        // Inverse the camera transformation to bring the world point into the camera's local space
        Vector3 localPoint = cameraTransformMatrix.inverse.MultiplyPoint3x4(worldPoint);

        // Project the local 3D point to the camera's 2D plane
        // Vector2 cameraPlanePoint = ProjectPointToCameraPlane(icp, localPoint);
        Vector2 cameraPlanePoint = ProjectPointToCameraPlane(icpFocalLength, icpPrincipalPoint, localPoint);

        // Convert camera plane coordinates to pixel coordinates
        // Vector2 pixelCoordinates = ConvertCameraPlanePointToPixel(icp, cameraPlanePoint);
        Vector2 pixelCoordinates = ConvertCameraPlanePointToPixel(icpWidth, icpHeight, icpPrincipalPoint, cameraPlanePoint);
        return pixelCoordinates;
    }

    /// <summary>
    /// Projects a point from 3D space onto the camera's 2D plane using the camera's intrinsic parameters.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="point">The point in the camera's local space.</param>
    /// <returns>The point on the camera plane.</returns>
    // private static Vector2 ProjectPointToCameraPlane(MLCamera.IntrinsicCalibrationParameters icp, Vector3 point)
    private static Vector2 ProjectPointToCameraPlane(Vector2 icpFocalLength, Vector2 icpPrincipalPoint, Vector3 point)
    {
        // Normalize the point by the depth to project it onto the camera plane
        Vector2 normalizedPoint = new Vector2(point.x / point.z, point.y / point.z);

        // Apply the camera's intrinsic parameters to map the normalized point to the camera plane
        /*
        Vector2 cameraPlanePoint = new Vector2(
            normalizedPoint.x * icp.FocalLength.x + icp.PrincipalPoint.x,
            normalizedPoint.y * icp.FocalLength.y + icp.PrincipalPoint.y
        );
        */
        Vector2 cameraPlanePoint = new Vector2(
            normalizedPoint.x * icpFocalLength.x + icpPrincipalPoint.x,
            normalizedPoint.y * icpFocalLength.y + icpPrincipalPoint.y
        );

        return cameraPlanePoint;
    }

    /// <summary>
    /// Converts a point from the camera plane to pixel coordinates.
    /// </summary>
    /// <param name="icp">Intrinsic Calibration parameters of the camera.</param>
    /// <param name="point">The point on the camera plane.</param>
    /// <returns>The corresponding pixel coordinates.</returns>
    // private static Vector2 ConvertCameraPlanePointToPixel(MLCamera.IntrinsicCalibrationParameters icp, Vector2 point)
    private static Vector2 ConvertCameraPlanePointToPixel(uint icpWidth, uint icpHeight, Vector2 icpPrincipalPoint, Vector2 point)
    {
        // Convert the camera plane point to pixel coordinates by accounting for the image dimensions
        /*
        Vector2 pixelCoordinates = new Vector2(
            (point.x - icp.PrincipalPoint.x + icp.Width / 2),
            (icp.Height / 2 - (point.y - icp.PrincipalPoint.y))
        );
        */
        Vector2 pixelCoordinates = new Vector2(
            (point.x - icpPrincipalPoint.x + icpWidth / 2),
            (icpHeight / 2 - (point.y - icpPrincipalPoint.y))
        );


        return pixelCoordinates;
    }
}