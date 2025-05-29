using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Synthesize : MonoBehaviour
{
    [Header("Generation Settings")]
     private int totalImages = 6000;
     private float trainSplit = 0.75f;
     private float validSplit = 0.2f;
    private string outputFolder = "SyntheticChessData";
    
    [Header("Chess Board Settings")]
    [SerializeField] private ChessBoardSetup[] chessBoards;
    private ChessBoardSetup activeBoard;
    
    [Header("Camera Settings")]
     [SerializeField] private Camera mainCamera;
     private float minDistanceFromBoard = 0.4f;
     private float maxDistanceFromBoard = 2.5f;
     private int renderWidth = 512;
     private int renderHeight = 512;
    
    [Header("Lighting Settings")]
    [SerializeField] private Light[] sceneLights;
     private float minIntensity = 1.0f;
     private float maxIntensity = 1.5f;
     private float ambientIntensity = 1.0f;
     private float minShadowStrength = 0.3f;
     private float maxShadowStrength = 0.8f;
     private float minShadowBias = 0.01f;
     private float maxShadowBias = 0.05f;
     private float minShadowNormalBias = 0.1f;
     private float maxShadowNormalBias = 0.4f;
    
    [Header("Skybox Settings")]
    [SerializeField] private Cubemap[] skyboxTextures;
    
    [Header("Post-Processing")]
    [SerializeField]private Volume postProcessVolume;
    private Bloom bloom;
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private FilmGrain filmGrain;

    private string[] splitFolders = { "train", "valid", "test" };

    // Add class mapping for YOLO format
    private Dictionary<string, int> classMapping = new Dictionary<string, int>
    {
        {"Black bishop", 0},  // b
        {"Black king", 1},    // k
        {"Black knight", 2},  // n
        {"Black pawn", 3},    // p
        {"Black queen", 4},   // q
        {"Black rook", 5},    // r
        {"White bishop", 6},  // B
        {"White king", 7},    // K
        {"White knight", 8},  // N
        {"White pawn", 9},    // P
        {"White queen", 10},  // Q
        {"White rook", 11}    // R
    };

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
        InitializePostProcessing();

        
        // Clean up old files
        CleanupOutputDirectory();

        // Create output directories
        CreateOutputDirectories();

        // Start generation
        GenerateDataset();
    }

    

    void InitializePostProcessing()
    {
        if (postProcessVolume == null)
        {
            postProcessVolume = FindFirstObjectByType<Volume>();
            if (postProcessVolume == null)
            {
                GameObject volumeObj = new GameObject("Post Process Volume");
                postProcessVolume = volumeObj.AddComponent<Volume>();
                postProcessVolume.isGlobal = true;
                postProcessVolume.priority = 1;
            }
        }
            
        if (postProcessVolume.profile == null)
        {
            postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        // Get or add post-processing effects
        postProcessVolume.profile.TryGet(out bloom);
        postProcessVolume.profile.TryGet(out colorAdjustments);
        postProcessVolume.profile.TryGet(out vignette);
        postProcessVolume.profile.TryGet(out chromaticAberration);
        postProcessVolume.profile.TryGet(out filmGrain);

        // if (!postProcessVolume.profile.TryGet(out bloom)) bloom = postProcessVolume.profile.Add<Bloom>(false);
        // if (!postProcessVolume.profile.TryGet(out colorAdjustments)) colorAdjustments = postProcessVolume.profile.Add<ColorAdjustments>(false);
        // if (!postProcessVolume.profile.TryGet(out vignette)) vignette = postProcessVolume.profile.Add<Vignette>(false);
        // if (!postProcessVolume.profile.TryGet(out chromaticAberration)) chromaticAberration = postProcessVolume.profile.Add<ChromaticAberration>(false);
        // if (!postProcessVolume.profile.TryGet(out filmGrain)) filmGrain = postProcessVolume.profile.Add<FilmGrain>(false);
    }

    void CleanupOutputDirectory()
    {
        string basePath = Path.Combine(Application.dataPath, "..", outputFolder);
        if (Directory.Exists(basePath))
        {
            Directory.Delete(basePath, true);
        }
    }

    void CreateOutputDirectories()
    {
        string basePath = Path.Combine(Application.dataPath, "..", outputFolder);
        
        foreach (string split in splitFolders)
        {
            string imagesPath = Path.Combine(basePath, split, "images");
            string labelsPath = Path.Combine(basePath, split, "labels");
            Directory.CreateDirectory(imagesPath);
            Directory.CreateDirectory(labelsPath);
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

        GenerateSplit("train", trainCount);
        GenerateSplit("valid", validCount);
        GenerateSplit("test", testCount);

        Debug.Log("Dataset generation complete!");
    }

    void GenerateSplit(string split, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Randomize scene
            RandomizeScene();
            
            // Capture and save image
            CaptureAndSave(split, i);
        }
    }

    List<ChessBoardSetup.PiecePlacement> GenerateRandomPosition()
    {
        // Start with a standard chess position
        char[,] board = new char[8, 8];
        string defaultFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        string[] ranks = defaultFEN.Split(' ')[0].Split('/');
        
        // Parse initial position
        for (int rank = 0; rank < 8; rank++)
        {
            int file = 0;
            foreach (char c in ranks[rank])
            {
                if (char.IsDigit(c))
                {
                    for (int i = 0; i < int.Parse(c.ToString()); i++)
                    {
                        board[rank, file++] = '.';
                    }
                }
                else
                {
                    board[rank, file++] = c;
                }
            }
        }
        
        // Generate random number of moves (0-50)
        int numMoves = Random.Range(0, 21);
        
        for (int move = 0; move < numMoves; move++)
        {
            // Find all valid moves
            List<(int fromRank, int fromFile, int toRank, int toFile)> validMoves = new List<(int, int, int, int)>();
            
            for (int fromRank = 0; fromRank < 8; fromRank++)
            {
                for (int fromFile = 0; fromFile < 8; fromFile++)
                {
                    char piece = board[fromRank, fromFile];
                    if (piece == '.') continue;
                    
                    // Only move pieces of the current player's color
                    bool isWhite = char.IsUpper(piece);
                    if ((move % 2 == 0 && !isWhite) || (move % 2 == 1 && isWhite)) continue;
                    
                    // Check all possible moves for this piece
                    for (int toRank = 0; toRank < 8; toRank++)
                    {
                        for (int toFile = 0; toFile < 8; toFile++)
                        {
                            char targetPiece = board[toRank, toFile];
                            
                            // Can't capture own pieces
                            if (targetPiece != '.' && char.IsUpper(targetPiece) == isWhite) continue;
                            
                            // Don't allow capturing kings
                            if (targetPiece == 'k' || targetPiece == 'K') continue;
                            
                            // Add move if it's valid
                            validMoves.Add((fromRank, fromFile, toRank, toFile));
                        }
                    }
                }
            }
            
            // If no valid moves, break
            if (validMoves.Count == 0) break;
            
            // Make a random move
            var (srcRank, srcFile, dstRank, dstFile) = validMoves[Random.Range(0, validMoves.Count)];
            board[dstRank, dstFile] = board[srcRank, srcFile];
            board[srcRank, srcFile] = '.';
        }
        
        // Ensure both kings are present
        bool hasWhiteKing = false;
        bool hasBlackKing = false;
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                if (board[rank, file] == 'K') hasWhiteKing = true;
                if (board[rank, file] == 'k') hasBlackKing = true;
            }
        }
        
        // If either king is missing, place them in their original positions
        if (!hasWhiteKing) board[7, 4] = 'K'; // e1
        if (!hasBlackKing) board[0, 4] = 'k'; // e8
        
        // Convert board to list of PiecePlacement
        List<ChessBoardSetup.PiecePlacement> placements = new List<ChessBoardSetup.PiecePlacement>();
        Dictionary<char, string> pieceMap = new Dictionary<char, string>
        {
            {'k', "Black king"},
            {'q', "Black queen"},
            {'r', "Black rook"},
            {'b', "Black bishop"},
            {'n', "Black knight"},
            {'p', "Black pawn"},
            {'K', "White king"},
            {'Q', "White queen"},
            {'R', "White rook"},
            {'B', "White bishop"},
            {'N', "White knight"},
            {'P', "White pawn"}
        };
        
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                char piece = board[rank, file];
                if (piece != '.' && pieceMap.TryGetValue(piece, out string pieceName))
                {
                    placements.Add(new ChessBoardSetup.PiecePlacement
                    {
                        PieceName = pieceName,
                        Position = new Vector2Int(file, 7 - rank) // Convert to 0-based coordinates
                    });
                }
            }
        }
        
        return placements;
    }

    void RandomizeScene()
    {
        // Randomly select a board
        int idx = Random.Range(0, chessBoards.Length);
        if (Random.value > 0.5f) {
            idx = 1;
        }
        for (int i = 0; i < chessBoards.Length; i++)
        {
            chessBoards[i].gameObject.SetActive(i == idx);
            if (i != idx)
            {
                chessBoards[i].DeactivateAllPieces(); // Deactivate all pieces on inactive boards
            }
        }
        activeBoard = chessBoards[idx];

        // Set up random chess position
        var placements = GenerateRandomPosition();
        activeBoard.SetupPosition(placements);

        // Randomize camera to look at the active board
        RandomizeCamera();

        // Randomize lighting
        RandomizeLighting();

        // Randomize skybox
        RandomizeSkybox();

        // Randomize post-processing
        RandomizePostProcessing();
    }

    void RandomizeCamera()
    {
        // Randomly choose white or black perspective
        bool isWhitePerspective = Random.value > 0.5f;
        
        // Base position for each perspective
        float baseDistance = Random.Range(minDistanceFromBoard, maxDistanceFromBoard);
        float baseHeight = Random.Range(1.4f, 1.8f); // Average human eye height
        float baseAngle = isWhitePerspective ? 90f : 270f; // 0 for white, 180 for black
        
        // Add random offsets for natural variation
        float distanceOffset = Random.Range(-0.3f, 0.3f);
        float heightOffset = Random.Range(-0.2f, 0.2f);
        float angleOffset = Random.Range(-10f, 10f); // Reduced angle variation
        float tiltOffset = Random.Range(-5f, 5f);
        
        // Calculate final position
        float finalDistance = baseDistance + distanceOffset;
        float finalHeight = baseHeight + heightOffset;
        float finalAngle = baseAngle + angleOffset;
        
        // Position camera behind the board
        Vector3 position = new Vector3(
            Mathf.Cos(finalAngle * Mathf.Deg2Rad) * finalDistance,
            finalHeight,
            Mathf.Sin(finalAngle * Mathf.Deg2Rad) * finalDistance
        );
        
        mainCamera.transform.position = position;
        
        // Look at the center of the board
        mainCamera.transform.LookAt(activeBoard.transform.position);
        
        // Add slight downward tilt for natural viewing angle
        float downwardTilt = Random.Range(5f, 15f);
        mainCamera.transform.RotateAround(mainCamera.transform.position, mainCamera.transform.right, downwardTilt);
        
        // Add random tilt for natural head position
        mainCamera.transform.RotateAround(mainCamera.transform.position, mainCamera.transform.right, tiltOffset);
        
        // Add slight random roll for natural head tilt
        float rollOffset = Random.Range(-2f, 2f);
        mainCamera.transform.RotateAround(mainCamera.transform.position, mainCamera.transform.forward, rollOffset);
    }

    void RandomizeLighting()
    {
        foreach (Light light in sceneLights)
        {
            if (light != null)
            {
                // Randomize intensity
                light.intensity = Random.Range(minIntensity, maxIntensity);

                // Vary color temperature
                light.useColorTemperature = true;
                light.colorTemperature = Random.Range(3500f, 7000f); // 3500K (warm) to 7000K (cool)
        
                
                // Slightly desaturate the light colors
                light.color = new Color(
                    Random.Range(0.9f, 1.2f),
                    Random.Range(0.9f, 1.2f),
                    Random.Range(0.9f, 1.2f)
                );

                // Randomize light position if it's a directional light
                if (light.type == LightType.Directional)
                {
                    // Random rotation around the board
                    float angle = Random.Range(0f, 360f);
                    float height = Random.Range(30f, 60f);
                    light.transform.rotation = Quaternion.Euler(height, angle, 0);
                }

                // Configure shadow settings
                light.shadows = LightShadows.Soft;
                light.shadowStrength = Random.Range(minShadowStrength, maxShadowStrength);
                light.shadowBias = Random.Range(minShadowBias, maxShadowBias);
                light.shadowNormalBias = Random.Range(minShadowNormalBias, maxShadowNormalBias);
                
                // Randomize shadow softness
                light.shadowRadius = Random.Range(1f, 3f);
                
                // Randomize shadow resolution
                int[] resolutions = { 1024, 2048, 4096 };
                light.shadowResolution = (LightShadowResolution)Random.Range(0, 3);
            }
        }
        
        // Set ambient lighting
        RenderSettings.ambientLight = new Color(ambientIntensity, ambientIntensity, ambientIntensity);
        
        // Randomize ambient occlusion
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = Random.Range(0.8f, 1.2f);
    }

    void RandomizeSkybox()
    {
        if (skyboxTextures != null && skyboxTextures.Length > 0)
        {
            RenderSettings.skybox.SetTexture("_Tex", skyboxTextures[Random.Range(0, skyboxTextures.Length)]);
        }
    }

    void RandomizePostProcessing()
    {
        if (bloom != null)
        {
            bloom.intensity.value = Random.Range(0.1f, 0.4f);
            bloom.threshold.value = Random.Range(0.7f, 1.2f);
        }
        
        if (colorAdjustments != null)
        {
            // Reduce exposure to prevent over-exposure
            colorAdjustments.postExposure.value = Random.Range(-0.5f, 0.1f);
            colorAdjustments.contrast.value = Random.Range(0f, 15f);
            
            // Random hue shift (-180 to 180 degrees)
            colorAdjustments.hueShift.value = Random.Range(-10f, 10f);
            
            // Random saturation (-100 to 0)
            colorAdjustments.saturation.value = Random.Range(-5f, 10f);
            
            // Random color filter
            Color randomColor = new Color(
                Random.Range(0.8f, 1.0f),  // Red
                Random.Range(0.8f, 1.0f),  // Green
                Random.Range(0.8f, 1.0f),  // Blue
                1.0f
            );
            colorAdjustments.colorFilter.value = randomColor;
        }
        
        if (vignette != null)
        {
            vignette.intensity.value = Random.Range(0.2f, 0.4f);
        }
        
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Random.Range(0.0f, 0.3f);
        }
        
        if (filmGrain != null)
        {
            var grainTypes = (FilmGrainLookup[])System.Enum.GetValues(typeof(FilmGrainLookup));
            filmGrain.type.value = grainTypes[Random.Range(0, grainTypes.Length)];
            filmGrain.intensity.value = Random.Range(0.0f, 0.4f);
        }
    }

    Bounds TransformBounds(Bounds localBounds, Transform transform)
    {
        Vector3 center = transform.TransformPoint(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 axisX = transform.TransformVector(extents.x, 0, 0);
        Vector3 axisY = transform.TransformVector(0, extents.y, 0);
        Vector3 axisZ = transform.TransformVector(0, 0, extents.z);
        Vector3 worldExtents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)
        );
        return new Bounds(center, worldExtents * 2);
    }

    void CaptureAndSave(string split, int index)
    {
        // Create render texture with matching anti-aliasing
        RenderTexture rt = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1; // Match camera's anti-aliasing
        mainCamera.targetTexture = rt;
        
        // Render
        mainCamera.Render();
        
        // Read pixels
        RenderTexture.active = rt;
        Texture2D screenshot = new Texture2D(renderWidth, renderHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
        screenshot.Apply();
        
        // Save image
        string imagePath = Path.Combine(Application.dataPath, "..", outputFolder, split, "images", $"image_{index:D6}.jpg");
        using (var fs = new FileStream(imagePath, FileMode.Create, FileAccess.Write))
        {
            byte[] pngData = screenshot.EncodeToJPG(90);
            fs.Write(pngData, 0, pngData.Length);
        }

        // Generate and save YOLO format labels
        List<string> yoloLabels = new List<string>();

        // Define multipliers for each piece type (for height only)
        Dictionary<string, float> pieceHeightMultipliers = new Dictionary<string, float>
        {
            {"king", 1.6f},
            {"queen", 1.5f},
            {"rook", 1.2f},
            {"bishop", 1.4f},
            {"knight", 1.1f},
            {"pawn", 0.8f}
        };

        float squareWidth = activeBoard.GetSquareWidth();

        foreach (var piece in activeBoard.GetActivePieces())
        {
            // Get all mesh renderers for the pieces
            //var meshRenderers = piece.GetComponentsInChildren<MeshRenderer>();
            Mesh mesh = piece.GetComponent<MeshFilter>().mesh;
            Bounds localBounds = mesh.bounds;
            Bounds bounds = TransformBounds(localBounds, piece.transform);

            // Cylinder parameters from mesh bounds
            //Vector3 baseCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 baseCenter = piece.transform.position;
            float height = 1.0f * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float radius = 0.9f *Mathf.Min(bounds.size.x, bounds.size.y, bounds.size.z) / 2f;

            // 4 points at the base
            Vector3 baseForward = baseCenter + activeBoard.transform.forward * radius;
            Vector3 baseBack = baseCenter - activeBoard.transform.forward * radius;
            Vector3 baseRight = baseCenter + activeBoard.transform.right * radius;
            Vector3 baseLeft = baseCenter - activeBoard.transform.right * radius;

            // 4 points at the top
            float radiusTop = 0.75f * radius;
            Vector3 up = Vector3.up * height + baseCenter;
            Vector3 topForward = up + activeBoard.transform.forward * radiusTop;
            Vector3 topBack = up - activeBoard.transform.forward * radiusTop;
            Vector3 topRight = up + activeBoard.transform.right * radiusTop;
            Vector3 topLeft = up - activeBoard.transform.right * radiusTop;

            // Project all 8 points to screen space
            Vector3[] screenPoints = new Vector3[8];
            screenPoints[0] = mainCamera.WorldToScreenPoint(baseForward);
            screenPoints[1] = mainCamera.WorldToScreenPoint(baseBack);
            screenPoints[2] = mainCamera.WorldToScreenPoint(baseRight);
            screenPoints[3] = mainCamera.WorldToScreenPoint(baseLeft);
            screenPoints[4] = mainCamera.WorldToScreenPoint(topForward);
            screenPoints[5] = mainCamera.WorldToScreenPoint(topBack);
            screenPoints[6] = mainCamera.WorldToScreenPoint(topRight);
            screenPoints[7] = mainCamera.WorldToScreenPoint(topLeft);

            // Find min/max screen coordinates
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool anyInFront = false;

            foreach (Vector3 point in screenPoints)
            {
                if (point.z > 0)
                {
                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxX = Mathf.Max(maxX, point.x);
                    maxY = Mathf.Max(maxY, point.y);
                    anyInFront = true;
                }
            }
            if (!anyInFront) continue;

            // Convert to normalized coordinates (0-1)
            float x_center = (minX + maxX) / 2f / renderWidth;
            float y_center = (minY + maxY) / 2f / renderHeight;
            y_center = 1.0f - y_center; // Flip for YOLO
            float width = (maxX - minX) / renderWidth;
            float heightBox = (maxY - minY) / renderHeight;

            if (x_center < 0.0f || x_center > 1.0f || y_center < 0.0f || y_center > 1.0f) {
                continue;
            }
            // Clamp values
            x_center = Mathf.Clamp01(x_center);
            y_center = Mathf.Clamp01(y_center);
            width = Mathf.Clamp01(width);
            heightBox = Mathf.Clamp01(heightBox);

            // Get class ID
            string pieceName = piece.name.Replace("(Clone)", "").Trim();
            if (classMapping.TryGetValue(pieceName, out int classId))
            {
                yoloLabels.Add($"{classId} {x_center:F6} {y_center:F6} {width:F6} {heightBox:F6}");
            }
        }

        // Save labels
        string labelPath = Path.Combine(Application.dataPath, "..", outputFolder, split, "labels", $"image_{index:D6}.txt");
        using (var sw = new StreamWriter(labelPath, false))
        {
            foreach (var line in yoloLabels)
                sw.WriteLine(line);
        }
        
        // Clean up
        RenderTexture.active = null;
        mainCamera.targetTexture = null;
        Destroy(rt);
        Destroy(screenshot);

        // Periodically unload unused assets and force GC
        if (index % 100 == 0)
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }
    }
}
