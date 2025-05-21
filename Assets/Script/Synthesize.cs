using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Synthesize : MonoBehaviour
{
    //[Header("Generation Settings")]
    private int totalImages = 20;
    private float trainSplit = 0.7f;
    private float validSplit = 0.2f;
    private string outputFolder = "SyntheticPoolData";
    
    ////[Header("Ball Settings")]
    private float tableWidth = 1.27f; // Standard pool table width
    private float tableLength = 2.54f; // Standard pool table length
    private float tableHeight = 0.805f; // Standard pool table height + ball radius
    private float ballRadius = 0.028575f; // Standard pool ball radius in meters
    
    //[Header("Cue Settings")]
    [SerializeField] private Transform cueStick; // Reference to the cue stick object
    private float minCueDistance = 0.03f; // Minimum distance from cue ball
    private float maxCueDistance = 0.25f; // Maximum distance from cue ball
    private float minCueAngle = -25.0f; // Minimum angle from horizontal
    private float maxCueAngle = -5.0f; // Maximum angle from horizontal

    //[Header("Camera Settings")]
    private Camera mainCamera;
    private float minPlayerHeight = 1.0f; // Minimum player height in meters
    private float maxPlayerHeight = 1.6f; // Maximum player height in meters
    private float minDistanceFromTable = 0.0f; // Minimum distance from table edge
    private float maxDistanceFromTable = 1.0f; // Maximum distance from table edge
    private float minLookAngle = -15f; // Minimum angle to look down
    private float maxLookAngle = 5f; // Maximum angle to look down
    private int renderWidth = 512; 
    private int renderHeight = 512;
    
    //[Header("Lighting Settings")]
    [SerializeField] private Light[] sceneLights;
    private float minIntensity = 1.0f; // Increased minimum intensity
    private float maxIntensity = 3.0f; // Increased maximum intensity
    private float ambientIntensity = 1.0f; // Added ambient intensity
    
    //[Header("Debug Settings")]
    
    //[Header("Motion Blur Settings")]
    private float motionBlurChance = 0.0f; // 50% chance of motion blur
    private float minForce = 10f;
    private float maxForce = 15f;
    
    private List<Transform> poolBalls = new List<Transform>();

    private List<Transform> tablePockets = new List<Transform>();
    private string[] splitFolders = { "train", "valid", "test" };

    [Header("Skybox Settings")]
    [SerializeField] private Cubemap[] skyboxTextures; // List of skybox cubemaps to choose from

    [Header("Table Settings")]
    [SerializeField] private Material tableMaterial; // Reference to the pool table material
    [SerializeField] private Texture2D[] tableTextures; // List of felt textures to choose from
    [SerializeField] private float minTableRoughness = 0.3f; // Minimum roughness for the table
    [SerializeField] private float maxTableRoughness = 0.7f; // Maximum roughness for the table

    [Header("Post-Processing")]
    private Volume postProcessVolume;
    private Bloom bloom;
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private FilmGrain filmGrain;


    void Start()
    {
        // Initialize camera settings
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Enable high-quality camera settings
        mainCamera.allowHDR = true;
        mainCamera.allowMSAA = true;
        mainCamera.allowDynamicResolution = true;
        mainCamera.forceIntoRenderTexture = true;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCamera.backgroundColor = Color.black;
        
        // Set up high-quality rendering
        mainCamera.renderingPath = RenderingPath.DeferredShading;
        mainCamera.useOcclusionCulling = true;
        mainCamera.allowHDR = true;
        mainCamera.allowMSAA = true;
        mainCamera.allowDynamicResolution = true;
        mainCamera.forceIntoRenderTexture = true;
        mainCamera.depthTextureMode = DepthTextureMode.DepthNormals;

        // Initialize post-processing
        if (postProcessVolume == null)
        {
            Debug.Log("Searching for Post Process Volume in scene...");
            postProcessVolume = FindFirstObjectByType<Volume>();
            if (postProcessVolume != null)
            {
                Debug.Log($"Found Post Process Volume: {postProcessVolume.name}");
            }
            else
            {
                Debug.LogError("No Post Process Volume found in scene!");
                // Create a new Volume if none exists
                GameObject volumeObj = new GameObject("Post Process Volume");
                postProcessVolume = volumeObj.AddComponent<Volume>();
                postProcessVolume.isGlobal = true;
                postProcessVolume.priority = 1;
                Debug.Log("Created new Post Process Volume");
            }
        }
            
        if (postProcessVolume != null)
        {
            Debug.Log($"Post Process Volume profile is {(postProcessVolume.profile != null ? "assigned" : "null")}");
            if (postProcessVolume.profile == null)
            {
                // Create a new profile if none exists
                postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Debug.Log("Created new Volume Profile");
            }

            Debug.Log("Attempting to get post-processing effects...");
            bool gotBloom = postProcessVolume.profile.TryGet(out bloom);
            bool gotColorAdjustments = postProcessVolume.profile.TryGet(out colorAdjustments);
            bool gotVignette = postProcessVolume.profile.TryGet(out vignette);
            bool gotCA = postProcessVolume.profile.TryGet(out chromaticAberration);
            bool gotGrain = postProcessVolume.profile.TryGet(out filmGrain);

            Debug.Log($"Got effects: Bloom={gotBloom}, ColorAdjustments={gotColorAdjustments}, Vignette={gotVignette}, CA={gotCA}, Grain={gotGrain}");

            // Add missing effects
            if (!gotBloom) bloom = postProcessVolume.profile.Add<Bloom>(true);
            if (!gotColorAdjustments) colorAdjustments = postProcessVolume.profile.Add<ColorAdjustments>(true);
            if (!gotVignette) vignette = postProcessVolume.profile.Add<Vignette>(true);
            if (!gotCA) chromaticAberration = postProcessVolume.profile.Add<ChromaticAberration>(true);
            if (!gotGrain) filmGrain = postProcessVolume.profile.Add<FilmGrain>(true);

            // // Set initial values to make effects obvious
            // if (bloom != null)
            // {
            //     bloom.intensity.value = 2.0f;
            //     bloom.threshold.value = 0.5f;
            //     bloom.scatter.value = 0.7f;
            // }
            // if (colorAdjustments != null)
            // {
            //     colorAdjustments.postExposure.value = 0f;
            //     colorAdjustments.contrast.value = 20f;
            //     colorAdjustments.colorFilter.value = new Color(0.8f, 0.8f, 1.2f); // Slight blue tint
            //     colorAdjustments.hueShift.value = 1.0f;
            //     colorAdjustments.saturation.value = 20f;
            // }
            // if (vignette != null)
            // {
            //     vignette.intensity.value = 0.5f;
            //     vignette.smoothness.value = 0.5f;
            // }
            // if (chromaticAberration != null)
            // {
            //     chromaticAberration.intensity.value = 1.0f;
            // }
            // if (filmGrain != null)
            // {
            //     filmGrain.intensity.value = 0.5f;
            //     filmGrain.type.value = FilmGrainLookup.Thin1;
            // }
        }

        // Find all pool balls
        foreach (Transform child in transform)
        {
            if (child.name.Contains("ball_"))
                poolBalls.Add(child);
        }

        // Find all table pockets
        foreach (Transform child in transform)
        {
            if (child.name.Contains("pocket"))
                tablePockets.Add(child);
        }
        

        // Clean up old files
        CleanupOutputDirectory();

        // Create output directories
        CreateOutputDirectories();

        // Start generation
        GenerateDataset();
    }

    void CleanupOutputDirectory()
    {
        string basePath = Path.Combine(Application.dataPath, "..", outputFolder);
        if (Directory.Exists(basePath))
        {
            //Debug.Log($"Cleaning up old files in: {basePath}");
            Directory.Delete(basePath, true);
        }
    }

    void CreateOutputDirectories()
    {
        string basePath = Path.Combine(Application.dataPath, "..", outputFolder);
        //Debug.Log($"Saving dataset to: {basePath}");
        
        foreach (string split in splitFolders)
        {
            string imagesPath = Path.Combine(basePath, split, "images");
            string labelsPath = Path.Combine(basePath, split, "labels");
            Directory.CreateDirectory(imagesPath);
            Directory.CreateDirectory(labelsPath);
            // Debug.Log($"Created directories for {split} split:");
            // Debug.Log($"  Images: {imagesPath}");
            // Debug.Log($"  Labels: {labelsPath}");
        }
    }

    void GenerateDataset()
    {
        if (totalImages < 100) {
            GenerateSplit("train", totalImages);
            return;
        }
        int trainCount = Mathf.FloorToInt(totalImages * trainSplit);
        int validCount = Mathf.FloorToInt(totalImages * validSplit);
        int testCount = totalImages - trainCount - validCount;

        // Debug.Log($"Starting dataset generation. Total images: {totalImages}");
        // Debug.Log($"Split: Train={trainCount}, Valid={validCount}, Test={testCount}");

        GenerateSplit("train", trainCount);
        GenerateSplit("valid", validCount);
        GenerateSplit("test", testCount);


        Debug.Log("Dataset generation complete!");
    }

    void GenerateSplit(string split, int count)
    {
        Debug.Log($"Generating {count} images for {split} split...");
        for (int i = 0; i < count; i++)
        {
            float progress = (float)i / count * 100f;
            
            // Hide balls and table every 30th image
            bool hideBalls = (i % 30 == 0);
            if (hideBalls)
            {
                // Hide all pool balls
                foreach (Transform ball in poolBalls)
                {
                    ball.gameObject.SetActive(false);
                }
                
                // Hide the billiard table mesh
                MeshRenderer tableMesh = GetComponent<MeshRenderer>();
                if (tableMesh != null)
                {
                    tableMesh.enabled = false;
                }
            }
            
            RandomizeScene();
            CaptureAndSave(split, i, hideBalls);
            
            // Show balls and table again if they were hidden
            if (hideBalls)
            {
                foreach (Transform ball in poolBalls)
                {
                    ball.gameObject.SetActive(true);
                }
                
                // Show the billiard table mesh
                MeshRenderer tableMesh = GetComponent<MeshRenderer>();
                if (tableMesh != null)
                {
                    tableMesh.enabled = true;
                }
            }
        }
    }

    void RandomizeScene()
    {
        // Randomize table appearance
        RandomizeTable();

        // Randomize skybox
        RandomizeSkybox();

        // Randomize ball positions
        RandomizeBallPositions();

        // Position and orient cue stick
        PositionCueStick();

        // Randomize camera position and rotation
        RandomizeCamera();
        
        // Randomize lighting
        RandomizeLighting();

        // Randomize post-processing
        RandomizePostProcessing();

        // Apply random motion blur
        if (Random.value < motionBlurChance)
        {
            ApplyMotionBlur();
        }
    }

    void RandomizeTable()
    {
        if (tableMaterial == null || tableTextures == null || tableTextures.Length == 0) return;

        // Select a random texture from the list
        Texture2D randomTexture = tableTextures[Random.Range(0, tableTextures.Length)];
        
        // Apply the new texture to the table material
        tableMaterial.SetTexture("_BaseMap", randomTexture);
        
        // Randomize roughness for variety in appearance
        float roughness = Random.Range(minTableRoughness, maxTableRoughness);
        tableMaterial.SetFloat("_Smoothness", 1f - roughness); // Convert roughness to smoothness
        
        // Randomize normal map intensity slightly for texture variation
        float normalIntensity = Random.Range(0.8f, 1.2f);
        tableMaterial.SetFloat("_BumpScale", normalIntensity);
    }

    void RandomizeSkybox()
    {
        if (skyboxTextures == null || skyboxTextures.Length == 0) return;

        // Get the current skybox material
        Material skyboxMaterial = RenderSettings.skybox;
        if (skyboxMaterial == null) return;

        // Select a random cubemap from the list
        Cubemap randomCubemap = skyboxTextures[Random.Range(0, skyboxTextures.Length)];
        
        // Apply the new cubemap to the skybox material
        skyboxMaterial.SetTexture("_Tex", randomCubemap);
        
        // Force Unity to update the skybox
        DynamicGI.UpdateEnvironment();
    }

    void PositionCueStick()
    {
        if (cueStick == null) return;

        // Find the cue ball (assuming it's the first ball in the list)
        Transform cueBall = poolBalls[0];
        
        // Random distance from cue ball
        float distance = Random.Range(minCueDistance, maxCueDistance);
        
        
        
        
        // Set cue position
        cueStick.position = cueBall.position;
        cueStick.GetChild(0).localPosition = new Vector3(Random.Range(-1.25f, -0.75f), cueStick.GetChild(0).localPosition.y, cueStick.GetChild(0).localPosition.z);
        
        // Calculate direction to cue ball
        //Vector3 directionToBall = (cueBall.position - cueStick.position).normalized;
        
        // Add random tilt
        float tiltAngle = Random.Range(minCueAngle, maxCueAngle);
        
        // Set rotation
        Quaternion baseRotation = Quaternion.AngleAxis(Random.Range(-60f, 60f), Vector3.up);
        Quaternion tiltRotation = Quaternion.AngleAxis(tiltAngle, Vector3.forward);
        cueStick.rotation = baseRotation * tiltRotation;
    }

    void RandomizeCamera()
    {
        // Get table center position
        Vector3 tableCenter = new Vector3(0, tableHeight, 0);
        
        // Random player height
        float playerHeight = Random.Range(minPlayerHeight, maxPlayerHeight);
        
        // Random position around the table center
        float angle = Random.Range(-180f, 180f); // Focus on a 360-degree arc
        float distance = Random.Range(minDistanceFromTable, maxDistanceFromTable);
        
        // Calculate position based on angle and distance from table center
        float x = Mathf.Cos(angle * Mathf.Deg2Rad) * distance;
        float z = Mathf.Sin(angle * Mathf.Deg2Rad) * distance;
        
        // Set camera position at player height
        mainCamera.transform.position = new Vector3(x, playerHeight, z);
        
        // Calculate look direction towards table center
        Vector3 lookDirection = tableCenter - mainCamera.transform.position;
        //lookDirection.y = 0; // Keep the look direction horizontal
        
        // Add random up/down angle
        float lookAngle = Random.Range(minLookAngle, maxLookAngle);
        
        // Set rotation
        mainCamera.transform.rotation = Quaternion.LookRotation(lookDirection) * Quaternion.Euler(lookAngle, 0, 0);

        // Set FOV and aspect ratio
        //mainCamera.fieldOfView = 76.66f; //Specs FOV
        //mainCamera.aspect = 1.333f; //Specs aspect

        //square aspect ratio
        mainCamera.fieldOfView = 61.33f; //Specs FOV
        mainCamera.aspect = 1.0f; //Specs aspect

        //Debug.Log($"Camera pos: {mainCamera.transform.position}, Looking at table center: {tableCenter}");
    }

    void RandomizeLighting()
    {
        // Get the URP asset to modify shadow settings
        var urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            // Make shadows darker
            urpAsset.shadowDistance = 2.0f; // Increase shadow distance
            urpAsset.shadowCascadeCount = 4; // Use 4 cascades for better quality
            urpAsset.shadowDepthBias = 1.0f; // Increase shadow depth bias
            urpAsset.shadowNormalBias = 1.0f; // Increase shadow normal bias
            //urpAsset.shadowSoftShadows = true; // Enable soft shadows
        }

        // Set ambient lighting
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = ambientIntensity;

        foreach (Light light in sceneLights)
        {
            if (light != null)
            {
                // Enhanced lighting settings
                light.intensity = Random.Range(minIntensity, maxIntensity);
                light.shadows = LightShadows.Soft; // Changed to soft shadows
                light.shadowStrength = Random.Range(0.6f, 0.8f); // Reduced shadow strength for softer shadows
                light.shadowBias = 0.02f; // Reduced bias for sharper shadows
                light.shadowNormalBias = 0.2f; // Reduced normal bias
                light.shadowNearPlane = 0.1f;
                
                // Warmer color temperature range for more natural lighting
                float temperature = Random.Range(3000f, 5500f); // Adjusted Kelvin temperature range
                Color color = Mathf.CorrelatedColorTemperatureToRGB(temperature);
                light.color = color;
                
                // Randomize light range and spot angle if it's a spot light
                if (light.type == LightType.Spot)
                {
                    light.range = Random.Range(8f, 12f); // Increased range
                    light.spotAngle = Random.Range(40f, 80f); // Increased spot angle
                }
                
                // Calculate random point on floor within 5m radius
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(0f, 5f);
                Vector3 targetPoint = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                );
                
                // Make light look at the target point
                light.transform.LookAt(targetPoint);
            }
        }
    }

    void RandomizeBallPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        
        foreach (Transform ball in poolBalls)
        {
            Vector3 newPos;
            bool validPosition;
            int attempts = 0;
            
            do
            {
                newPos = new Vector3(
                    Random.Range(-tableLength/2.0f + ballRadius, tableLength/2.0f - ballRadius),
                    tableHeight,
                    Random.Range(-tableWidth/2.0f + ballRadius, tableWidth/2.0f - ballRadius)
                );
                
                validPosition = true;
                foreach (Vector3 pos in positions)
                {
                    if (Vector3.Distance(newPos, pos) < ballRadius*2.0f)
                    {
                        validPosition = false;
                        break;
                    }
                }
                
                attempts++;
            } while (!validPosition && attempts < 100);
            
            if (validPosition)
            {
                ball.localPosition = newPos;
                ball.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                positions.Add(newPos);
            }
        }
    }

    void ApplyMotionBlur()
    {
        foreach (Transform ball in poolBalls)
        {
            // Add Rigidbody if it doesn't exist
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = ball.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.17f; // Standard pool ball mass
                rb.linearDamping = 0.1f;
                rb.angularDamping = 0.1f;
                rb.interpolation = RigidbodyInterpolation.Interpolate; // Enable interpolation for smoother motion
            }

            // Apply random force
            float force = Random.Range(minForce, maxForce);
            float angle = Random.Range(0f, 360f);
            Vector3 forceDirection = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                0,
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
            rb.AddForce(forceDirection * force, ForceMode.Impulse);
        }
    }

    void RandomizePostProcessing()
    {
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            // Randomize bloom
            // if (bloom != null)
            // {
            //     bloom.intensity.value = Random.Range(0.0f, 1.5f);
            //     bloom.threshold.value = Random.Range(0.7f, 1.2f);
            //     bloom.scatter.value = Random.Range(0.5f, 0.7f);
            // }

            // Randomize color adjustments
            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.value = Random.Range(0f, 0.5f);
                colorAdjustments.contrast.value = Random.Range(-5f, 25f);
                // colorAdjustments.colorFilter.value = new Color(
                //     Random.Range(0.8f, 1.2f),  // R
                //     Random.Range(0.8f, 1.2f),  // G
                //     Random.Range(0.8f, 1.2f)   // B 
                // );
                colorAdjustments.hueShift.value = Random.Range(-2f, 2f);
                colorAdjustments.saturation.value = Random.Range(-5f, 5f);
                //colorAdjustments.temperature.value = Random.Range(0.0f, 1.0f);
                //colorAdjustments.tint.value = Random.Range(0.0f, 1.0f);
                //colorAdjustments.brighter.value = Random.Range(-1.0f, 1.0f);
               
            }

            // Subtle vignette
            // if (vignette != null)
            // {
            //     vignette.intensity.value = Random.Range(0.2f, 0.4f);
            //     vignette.smoothness.value = Random.Range(0.2f, 0.4f);
            // }

            // Subtle chromatic aberration
            // if (chromaticAberration != null)
            // {
            //     print("chromatic aberration");
            //     chromaticAberration.intensity.value =  Random.Range(0.0f, 0.3f);
            // }

            // Subtle film grain
            if (filmGrain != null)
            {
                filmGrain.intensity.value = Random.Range(0.0f, 0.6f);
                filmGrain.type.value = (FilmGrainLookup)Random.Range(0, 3);
            }
        }
        else
        {
            Debug.LogError("Post Process Volume or profile is null!");
        }
    }

    void CaptureAndSave(string split, int index, bool hideBalls = false)
    {
        // Create render texture with HDR support
        RenderTexture rt = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        rt.enableRandomWrite = true;
        
        // Store original camera settings
        RenderTexture originalRT = mainCamera.targetTexture;
        bool originalHDR = mainCamera.allowHDR;
        
        // Set up camera for capture
        mainCamera.targetTexture = rt;
        mainCamera.allowHDR = true;
        
        // Ensure post-processing is enabled
        UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
        }
        
        // Render the scene
        mainCamera.Render();
        
        // Create a temporary texture to read the render texture
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        // Save the image
        string imagePath = Path.Combine(Application.dataPath, "..", outputFolder, split, "images", $"image_{index}.jpg");
        byte[] bytes = tex.EncodeToJPG(90);
        File.WriteAllBytes(imagePath, bytes);
        
        // Cleanup
        Destroy(tex);
        mainCamera.targetTexture = originalRT;
        mainCamera.allowHDR = originalHDR;
        Destroy(rt);
        RenderTexture.active = null;
        
        // Generate and save YOLO format labels
        string labelPath = Path.Combine(Application.dataPath, "..", outputFolder, split, "labels", $"image_{index}.txt");
        SaveYOLOLabels(labelPath, hideBalls);
        
        // Reset ball velocities
        foreach (Transform ball in poolBalls)
        {
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    // void SaveRenderTexture(RenderTexture rt, string path)
    // {
    //     RenderTexture.active = rt;
    //     Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
    //     tex.filterMode = FilterMode.Trilinear;
    //     tex.anisoLevel = 9;
    //     tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
    //     tex.Apply();
        
    //     byte[] bytes = tex.EncodeToJPG(80); // Save as JPEG with 80% quality
    //     File.WriteAllBytes(path, bytes);
        
    //     Destroy(tex);
    //     RenderTexture.active = null;
    // }

    void SaveYOLOLabels(string path, bool hideBalls = false)
    {
        List<string> labels = new List<string>();
        
        if (!hideBalls)
        {
            for (int i = 0; i < poolBalls.Count; i++)
            {
                Transform ball = poolBalls[i];
                Vector3 screenPoint = mainCamera.WorldToViewportPoint(ball.position);
                
                // Skip if ball is behind camera or outside viewport
                if (screenPoint.z < 0 || screenPoint.x < 0 || screenPoint.x > 1 || screenPoint.y < 0 || screenPoint.y > 1)
                    continue;
                
                // Convert to YOLO format (x_center, y_center, width, height)
                float x = screenPoint.x;
                float y = 1.0f - screenPoint.y;

                // Get points offset by ball radius in screen space
                Vector3 leftPoint = ball.position - mainCamera.transform.right * ballRadius;
                Vector3 rightPoint = ball.position + mainCamera.transform.right * ballRadius;
                Vector3 upPoint = ball.position + mainCamera.transform.up * ballRadius;
                Vector3 downPoint = ball.position - mainCamera.transform.up * ballRadius;

                // Convert to screen space
                Vector3 leftScreen = mainCamera.WorldToViewportPoint(leftPoint);
                Vector3 rightScreen = mainCamera.WorldToViewportPoint(rightPoint);
                Vector3 upScreen = mainCamera.WorldToViewportPoint(upPoint);
                Vector3 downScreen = mainCamera.WorldToViewportPoint(downPoint);

                // Calculate normalized width and height
                float normalizedWidth = Mathf.Abs(rightScreen.x - leftScreen.x);
                float normalizedHeight = Mathf.Abs(upScreen.y - downScreen.y);
                
                // Add class ID using the index of the ball
                string label = $"{i} {x} {y} {normalizedWidth} {normalizedHeight}";
                labels.Add(label);
            }

            // Add pocket bounding boxes
            if (tablePockets != null)
            {
                int pocketClassId = poolBalls.Count; // Next class after balls
                float pocketBoxSize = 0.05f; // Adjust as needed

                foreach (Transform pocket in tablePockets)
                {
                    Vector3 screenPoint = mainCamera.WorldToViewportPoint(pocket.position);

                    // Skip if pocket is behind camera or outside viewport
                    if (screenPoint.z < 0 || screenPoint.x < 0 || screenPoint.x > 1 || screenPoint.y < 0 || screenPoint.y > 1)
                        continue;

                    float x = screenPoint.x;
                    float y = 1.0f - screenPoint.y;

                    string label = $"{pocketClassId} {x} {y} {pocketBoxSize} {pocketBoxSize}";
                    labels.Add(label);
                }
            }
        }
        
        File.WriteAllLines(path, labels);
    }


}
