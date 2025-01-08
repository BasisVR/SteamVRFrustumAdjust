using UnityEngine;
using Valve.VR;
using UnityEngine.XR;
using UnityEngine.Rendering;

//Place this script on the Camera (eye) object.
//for canted headsets like pimax, calculate proper culling matrix to avoid objects being culled too early at far edges
//prevents objects popping in and out of view
public static class SteamVRFrustumAdjust
{
    public static bool isCantedFov = false;
    public static Camera Camera;
    public static Matrix4x4 projectionMatrix;

    public static void OnEnable()
    {
        Initalize();
    }
    public static void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_beginCameraRendering;
        if (isCantedFov)
        {
            Debug.Log("isCantedFov of steamvr frustum adjust!");
            isCantedFov = false;
            Camera.ResetCullingMatrix();
        }
        Debug.Log("disable of steamvr frustum adjust!");
    }
    public static void Initalize()
    {
        Debug.Log("Initalization of steamvr frustum adjust!");
        RenderPipelineManager.beginCameraRendering += RenderPipelineManager_beginCameraRendering;
        bool isMultipass = (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass);
        HmdMatrix34_t eyeToHeadL = SteamVR.instance.hmd.GetEyeToHeadTransform(EVREye.Eye_Left);
        if (eyeToHeadL.m0 < 1)  //m0 = 1 for parallel projections
        {
            Debug.Log("using steamvr frustum adjust!");
            isCantedFov = true;
            float l_left = 0.0f, l_right = 0.0f, l_top = 0.0f, l_bottom = 0.0f;
            SteamVR.instance.hmd.GetProjectionRaw(EVREye.Eye_Left, ref l_left, ref l_right, ref l_top, ref l_bottom);
            float eyeYawAngle = Mathf.Acos(eyeToHeadL.m0);  //since there are no x or z rotations, this is y only. 10 deg on Pimax
            if (isMultipass) eyeYawAngle *= 2;  //for multipass left eye frustum is used twice? causing right eye to end up 20 deg short
            float eyeHalfFov = Mathf.Atan(SteamVR.instance.tanHalfFov.x);
            float tanCorrectedEyeHalfFovH = Mathf.Tan(eyeYawAngle + eyeHalfFov);

            //increase horizontal fov by the eye rotation angles
            projectionMatrix.m00 = 1 / tanCorrectedEyeHalfFovH;  //m00 = 0.1737 for Pimax

            //because of canting, vertical fov increases towards the corners. calculate the new maximum fov otherwise culling happens too early at corners
            float eyeFovLeft = Mathf.Atan(-l_left);
            float tanCorrectedEyeHalfFovV = SteamVR.instance.tanHalfFov.y * Mathf.Cos(eyeFovLeft) / Mathf.Cos(eyeFovLeft + eyeYawAngle);
            projectionMatrix.m11 = 1 / tanCorrectedEyeHalfFovV;   //m11 = 0.3969 for Pimax

            //set the near and far clip planes
            projectionMatrix.m22 = -(Camera.farClipPlane + Camera.nearClipPlane) / (Camera.farClipPlane - Camera.nearClipPlane);
            projectionMatrix.m23 = -2 * Camera.farClipPlane * Camera.nearClipPlane / (Camera.farClipPlane - Camera.nearClipPlane);
            projectionMatrix.m32 = -1;
        }
        else
        {
            isCantedFov = false;
        }
    }
    public static void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (isCantedFov)
        {
            camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;
        }
    }
}
