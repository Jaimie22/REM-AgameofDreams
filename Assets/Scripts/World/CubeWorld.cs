
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the cube root object (pivot at the cube centre).
// Handles unlocking faces, generating levels, spinning the cube, and switching views.
public class CubeWorld : MonoBehaviour
{
    public static CubeWorld Instance { get; private set; }

    public enum Mode { Overview, Map, Transitioning, Playing }

    [Header("References")]
    [Tooltip("The six faces, in level order (Element 0 = Level 1).")]
    public CubeFace[] faces = new CubeFace[6];
    public DreamCamera dreamCamera;
    public PlayerController player;
    [Tooltip("Optional. Leave empty and one is created automatically.")]
    public ScreenFader screenFader;
    [Tooltip("Optional. Leave empty and one is created automatically.")]
    public DetectionIndicator detectionIndicator;
    public VitalsUI vitalsUI;
    [Tooltip("Optional. Handles fog and lighting changes between faces.")]
    public MoodController moodController;

    [Tooltip("Optional. The gradient backdrop shown in the cube view.")]
    public BackgroundGradient backgroundGradient;

    [Tooltip("Optional. The giant dreamer standing behind the cube.")]
    public DreamerBackdrop dreamerBackdrop;

    [Header("Start")]
    [Tooltip("Which level the game starts on.")]
    [Range(1, 6)] public int startLevel = 1;
    [Tooltip("Seconds to fade in from black when the game starts.")]
    public float startFadeDuration = 1.5f;

    [Header("Level Generation")]
    [Tooltip("Build a brand new layout every time a face is entered.")]
    public bool regenerateOnEveryEntry = false;

    [Header("Map View (M key)")]
    [Tooltip("Seconds the cube takes to turn back when returning to the level.")]
    public float mapReturnDuration = 0.6f;

    [Header("Cube View Controls")]
    [Tooltip("How fast the cube spins when you drag with the mouse.")]
    public float dragRotateSpeed = 0.3f;
    [Tooltip("Max seconds between two clicks to count as a double-click.")]
    public float doubleClickTime = 0.3f;

    [Header("Cube View Music")]
    [Tooltip("Plays while the cube or map is on screen, and fades away when a level starts. Leave empty for none.")]
    public AudioClip cubeViewMusic;
    [Range(0f, 1f)] public float cubeViewMusicVolume = 0.35f;
    [Tooltip("Seconds the music takes to fade in and out.")]
    public float musicFadeTime = 1.5f;

    [Header("Transitions")]
    [Tooltip("Seconds it takes the cube to turn a face to the top.")]
    public float faceTurnDuration = 1.2f;

    [Header("Crystals")]
    [Tooltip("Each level's entry animation can play, with crystals building the level.")]
    public bool entryAnimations = true;
    [Tooltip("The colour of the crystals in each level, in level order. The crystals start in the colour of the level before and settle into the new one.")]
    public Color[] crystalTints = new Color[]
    {
        new Color(0.70f, 0.80f, 1.00f),   // Level 1, pale blue
        new Color(1.00f, 0.85f, 0.50f),   // Level 2, desert gold
        new Color(0.75f, 0.60f, 1.00f),   // Level 3, violet
        new Color(1.00f, 0.65f, 0.35f),   // Level 4, copper
        new Color(0.55f, 1.00f, 0.85f),   // Level 5, soft teal
        new Color(1.00f, 0.95f, 0.85f)    // Level 6, warm white
    };

    [Tooltip("Seconds the camera takes to glide from the overhead view to the player camera as the player arrives.")]
    public float arrivalCameraTime = 1.6f;

    Color TintOf(int index)
    {
        if (crystalTints == null || crystalTints.Length == 0) return new Color(0.7f, 0.8f, 1f);
        return crystalTints[Mathf.Clamp(index, 0, crystalTints.Length - 1)];
    }

    [Header("Safety")]
    [Tooltip("If the player falls this far below the face, they respawn.")]
    public float fallResetDistance = 10f;

    [Header("Testing")]
    [Tooltip("Unlock every face so you can jump anywhere while greyboxing.")]
    public bool unlockAllFaces = false;

    public Mode CurrentMode { get; private set; }
    public int CurrentFaceIndex { get; private set; } = -1;

    int highestUnlocked = 0;
    int lastClickedFace = -1;
    float lastClickTime;
    int[] faceSeeds;

    // Where the player was standing before leaving the level.
    // Stored relative to the face, because the cube can be spun while away.
    bool hasSavedSpot;
    Vector3 savedLocalPosition;
    Quaternion savedLocalRotation;

    AudioSource musicSource;

    void Awake()
    {
        Instance = this;

        if (player == null)
        {
            Debug.LogError("CubeWorld: the Player slot is empty. Drag in the player from the scene, or your player prefab.");
            return;
        }

        // A prefab is only a file on disk, so it can't be played with. Make a copy in the scene.
        if (!player.gameObject.scene.IsValid())
            player = Instantiate(player);

        // A prefab can't point at the scene's camera, so give the copy the camera it needs
        if (player.cameraTransform == null && dreamCamera != null)
            player.cameraTransform = dreamCamera.transform;
    }

    void Start()
    {
        // Create a fader automatically if none was assigned
        if (screenFader == null)
            screenFader = gameObject.AddComponent<ScreenFader>();

        // The green / amber / red light showing how aware the hunter is, also created automatically
        if (detectionIndicator == null)
            detectionIndicator = gameObject.AddComponent<DetectionIndicator>();

        // Hearts, energy and the objective, also created automatically
        if (vitalsUI == null)
            vitalsUI = GetComponent<VitalsUI>();
        if (vitalsUI == null)
            vitalsUI = gameObject.AddComponent<VitalsUI>();

        // The music for the cube view, silent until it's wanted
        if (cubeViewMusic != null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = cubeViewMusic;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        // One seed per face, rolled fresh for this playthrough
        faceSeeds = new int[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            faceSeeds[i] = Random.Range(int.MinValue, int.MaxValue);
            GenerateFace(i);
        }

        // Starting on a later level counts as having unlocked everything before it
        int startIndex = Mathf.Clamp(startLevel - 1, 0, faces.Length - 1);
        highestUnlocked = startIndex;

        SetUpEntryAnimations();

        // Every level before the one we're starting on counts as finished, so it shows its crystal
        for (int i = 0; i < startIndex; i++)
            faces[i].SetCompleted(true, TintOf(i));

        for (int i = 0; i < faces.Length; i++)
            faces[i].SetUnlocked(unlockAllFaces || i <= highestUnlocked);

        StartCoroutine(StartSequence(startIndex));
    }

    // Gives every level without its own entry animation the crystal one, in a style that suits it
    void SetUpEntryAnimations()
    {
        if (!entryAnimations) return;

        foreach (CubeFace face in faces)
        {
            if (face == null || face.GetComponent<IFaceIntro>() != null) continue;

            GenericFaceIntro intro = face.gameObject.AddComponent<GenericFaceIntro>();

            if (face.GetComponent<BiomeGenerator>() != null) intro.style = GenericFaceIntro.Style.Rain;
            else if (face.GetComponent<GearGenerator>() != null) intro.style = GenericFaceIntro.Style.Helix;
            else if (face.GetComponent<PlatformGenerator>() != null) intro.style = GenericFaceIntro.Style.Float;
            else if (face.GetComponent<AwakeningGenerator>() != null) intro.style = GenericFaceIntro.Style.Outward;
            else intro.style = GenericFaceIntro.Style.Arc;
        }
    }

    // A crystal drops onto the start and breaks open, and the player steps out of it.
    // The camera glides from the overhead view to the player camera while it happens.
    IEnumerator PlayerArrival(Color tint)
    {
        Transform ground = player.transform;
        Vector3 up = faces[CurrentFaceIndex].transform.up;
        Vector3 size = new Vector3(0.9f, 2.4f, 0.9f);

        Material mat = CrystalKit.Make(tint, 0.6f);
        Transform crystal = CrystalKit.Pillar("SpawnCrystal", mat, size).transform;

        Vector3 landing = ground.position + up * 1.3f;
        Vector3 top = landing + up * 12f;

        // The camera starts heading for the player as the crystal starts to fall
        dreamCamera.ClearIntroPose(arrivalCameraTime);

        float fall = 0.9f;
        for (float t = 0f; t < fall; t += Time.deltaTime)
        {
            float u = t / fall;
            crystal.position = Vector3.Lerp(top, landing, u * u);
            crystal.rotation = Quaternion.AngleAxis(u * 540f, up);
            yield return null;
        }
        crystal.position = landing;

        // It breaks open: the crystal shrinks away and the player grows into place
        Vector3 playerScale = ground.localScale;
        player.SetVisible(true);

        float open = 0.5f;
        for (float t = 0f; t < open; t += Time.deltaTime)
        {
            float p = t / open;
            float k = p - 1f;
            float back = 1f + 2.70158f * k * k * k + 1.70158f * k * k;   // eases past full size, then settles

            crystal.localScale = size * (1f - p);
            crystal.rotation = Quaternion.AngleAxis(540f + p * 360f, up);
            ground.localScale = playerScale * Mathf.Max(0.01f, back);
            yield return null;
        }

        ground.localScale = playerScale;
        Destroy(crystal.gameObject);
        Destroy(mat);
    }

    // The finished-level crystals show above the faces while the cube or map is on screen
    void UpdateBeacons()
    {
        bool show = CurrentMode == Mode.Overview || CurrentMode == Mode.Map;
        foreach (CubeFace face in faces)
            if (face != null) face.SetBeaconVisible(show);
    }

    // Builds (or rebuilds) one face's layout, whichever generator it uses
    void GenerateFace(int index)
    {
        LevelGenerator generator = faces[index].GetComponent<LevelGenerator>();
        if (generator != null) generator.Generate(faceSeeds[index]);

        MazeGenerator maze = faces[index].GetComponent<MazeGenerator>();
        if (maze != null) maze.Generate(faceSeeds[index]);

        BiomeGenerator biome = faces[index].GetComponent<BiomeGenerator>();
        if (biome != null) biome.Generate(faceSeeds[index]);

        GearGenerator gears = faces[index].GetComponent<GearGenerator>();
        if (gears != null) gears.Generate(faceSeeds[index]);

        AwakeningGenerator awakening = faces[index].GetComponent<AwakeningGenerator>();
        if (awakening != null) awakening.Generate(faceSeeds[index]);

        PlatformGenerator platforms = faces[index].GetComponent<PlatformGenerator>();
        if (platforms != null) platforms.Generate(faceSeeds[index]);

        Physics.SyncTransforms(); // make sure the new colliders are in place
    }

    // Applies this level's gravity and jump height
    void ApplyLevelRules(int index)
    {
        LevelTheme levelTheme = null;

        LevelGenerator generator = faces[index].GetComponent<LevelGenerator>();
        if (generator != null) levelTheme = generator.theme;

        MazeGenerator maze = faces[index].GetComponent<MazeGenerator>();
        if (maze != null && maze.theme != null) levelTheme = maze.theme;

        BiomeGenerator biome = faces[index].GetComponent<BiomeGenerator>();
        if (biome != null && biome.theme != null) levelTheme = biome.theme;

        GearGenerator gears = faces[index].GetComponent<GearGenerator>();
        if (gears != null && gears.theme != null) levelTheme = gears.theme;

        AwakeningGenerator awakening = faces[index].GetComponent<AwakeningGenerator>();
        if (awakening != null && awakening.theme != null) levelTheme = awakening.theme;

        PlatformGenerator platforms = faces[index].GetComponent<PlatformGenerator>();
        if (platforms != null && platforms.theme != null) levelTheme = platforms.theme;

        // The footsteps and other sounds follow the level's surface
        player.Audio.SetSurface(levelTheme);

        if (levelTheme == null) return;
        player.ApplyMovementSettings(levelTheme.gravity, levelTheme.jumpHeight);
    }

    // Gives the player the camera this face asks for (isometric or third person)
    void ApplyCameraStyle(int index)
    {
        if (dreamCamera == null) return;
        dreamCamera.playerCameraStyle = faces[index].cameraStyle;
    }

    // Tells LevelObjectives which face's fragments and door are in play
    void ApplyObjectives(int index)
    {
        LevelObjectives.Instance?.SetActiveFace(faces[index].transform);
    }

    // Fades the cube view music in while the cube or map is showing, and out otherwise
    void UpdateMusic()
    {
        if (musicSource == null) return;

        bool wanted = CurrentMode == Mode.Overview || CurrentMode == Mode.Map;
        float target = wanted ? cubeViewMusicVolume : 0f;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.1f, musicFadeTime);

        musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, step * Mathf.Max(cubeViewMusicVolume, 0.1f));
    }

    void Update()
    {
        UpdateMusic();

        UpdateBeacons();

        if (CurrentMode == Mode.Transitioning) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        // M toggles the map from anywhere
        if (kb != null && kb.mKey.wasPressedThisFrame)
        {
            ToggleMap();
            return;
        }

        // Escape pulls out to the cube, where it can be spun. Pressing it again goes back in.
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (CurrentMode == Mode.Playing) GoToCubeView();
            else ReturnToLevel();
            return;
        }

        if (mouse == null) return;
        float scroll = mouse.scroll.ReadValue().y;

        switch (CurrentMode)
        {
            case Mode.Playing:
                // The scroll wheel zooms the camera while playing (see DreamCamera).
                // To reach the cube, press M for the map, then scroll down.

                // Respawn if the player falls off the level.
                // Not on the final face, where falling off the edge is the ending.
                CubeFace face = faces[CurrentFaceIndex];
                bool isEnding = face.GetComponent<AwakeningGenerator>() != null;

                if (!isEnding &&
                    player.transform.position.y < face.transform.position.y - fallResetDistance)
                    player.Spawn(face.spawnPoint);
                break;

            case Mode.Map:
                // The map is fixed on one face, so there's no spinning here.
                // Scroll down steps out to the cube, scroll up goes back into the level.
                if (scroll < 0f) GoToCubeView();
                else if (scroll > 0f) ReturnToLevel();
                break;

            case Mode.Overview:
                // Hold left mouse and drag to spin the cube
                if (mouse.leftButton.isPressed)
                    DragRotate(mouse.delta.ReadValue());

                // Double-click a face to enter it
                if (mouse.leftButton.wasPressedThisFrame)
                    HandleClick(mouse.position.ReadValue());

                // Scroll up = enter whichever face is pointing at the camera
                if (scroll > 0f)
                    TryEnterFace(GetFaceFacingCamera());
                break;
        }
    }

    // M: open the map, or close it and go back to where the player was
    void ToggleMap()
    {
        if (CurrentMode == Mode.Playing) GoToMapView();
        else ReturnToLevel();
    }

    // Straight down onto the face the player is on
    public void GoToMapView()
    {
        if (CurrentFaceIndex < 0) return;

        SaveCurrentSpot();
        player.Hide();
        dreamCamera.SetMapView(faces[CurrentFaceIndex].transform);

        backgroundGradient?.SetVisible(true);
        dreamerBackdrop?.SetVisible(true);
        moodController?.SetAwayFromLevel(true);

        CurrentMode = Mode.Map;
    }

    // Pull back to the whole cube, where it can be spun and faces picked
    public void GoToCubeView()
    {
        SaveCurrentSpot();
        player.Hide();
        dreamCamera.SetOverview();

        backgroundGradient?.SetVisible(true);
        dreamerBackdrop?.SetVisible(true);
        moodController?.SetAwayFromLevel(true);

        CurrentMode = Mode.Overview;
    }

    // Back into the level, exactly where the player left off
    void ReturnToLevel()
    {
        if (!hasSavedSpot || CurrentFaceIndex < 0) return;
        StartCoroutine(ReturnToSavedSpot());
    }

    // Remembers where the player is, relative to the face they're on
    void SaveCurrentSpot()
    {
        if (CurrentFaceIndex < 0 || player == null) return;
        if (CurrentMode != Mode.Playing) return; // already saved on the way out

        Transform face = faces[CurrentFaceIndex].transform;
        savedLocalPosition = face.InverseTransformPoint(player.transform.position);
        savedLocalRotation = Quaternion.Inverse(face.rotation) * player.transform.rotation;
        hasSavedSpot = true;
    }

    // Turns the cube back and drops the player exactly where they left off
    IEnumerator ReturnToSavedSpot()
    {
        CurrentMode = Mode.Transitioning;
        CubeFace face = faces[CurrentFaceIndex];

        // The fog rolls back in as the cube turns
        backgroundGradient?.SetVisible(false);
        dreamerBackdrop?.SetVisible(false);
        moodController?.SetAwayFromLevel(false);

        // The cube may have been spun while away, so turn it back
        Quaternion start = transform.rotation;
        Quaternion target = Quaternion.Inverse(face.transform.localRotation);

        float duration = Mathf.Max(0.01f, mapReturnDuration);
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            transform.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.rotation = target;
        Physics.SyncTransforms();

        // Convert the saved spot back into world space now the cube has settled
        Vector3 worldPos = face.transform.TransformPoint(savedLocalPosition);
        Quaternion worldRot = face.transform.rotation * savedLocalRotation;

        ApplyLevelRules(CurrentFaceIndex);
        ApplyObjectives(CurrentFaceIndex);

        player.Spawn(worldPos, worldRot);
        ApplyCameraStyle(CurrentFaceIndex);
        dreamCamera.SetPlaying(player.transform);

        CurrentMode = Mode.Playing;

       
        
    }

    // Game start: place everything instantly while the screen is black, then fade in
    IEnumerator StartSequence(int index)
    {
        CurrentMode = Mode.Transitioning;
        screenFader.SetAlpha(1f);

        CurrentFaceIndex = index;
        CubeFace face = faces[index];

        // Snap the cube so this face is on top
        transform.rotation = Quaternion.Inverse(face.transform.localRotation);
        Physics.SyncTransforms();

        // A level with an entry animation starts empty, and builds itself in front of you
        IFaceIntro intro = entryAnimations ? face.GetComponent<IFaceIntro>() : null;
        if (intro != null) intro.HideForIntro();

        // The game opens inside a level, not in the cube view
        backgroundGradient?.SetVisible(false);
        dreamerBackdrop?.SetVisible(false);
        moodController?.SetAwayFromLevel(false);

       
        ApplyLevelRules(index);
        ApplyObjectives(index);

        // A new level starts with full hearts and full energy
        player.Vitals.RestoreAll();

        player.Spawn(face.spawnPoint);
        ApplyCameraStyle(index);
        dreamCamera.SetPlaying(player.transform);
        dreamCamera.SnapToTarget();
        ApplyMood(index, true);

        yield return null; // let one frame settle before revealing

        if (intro != null)
        {
            // Fade in from black while the crystals gather, and keep control until the level is built
            player.frozen = true;
            player.SetVisible(false);
            StartCoroutine(screenFader.Fade(1f, 0f, startFadeDuration));
            Color tint = TintOf(index);
            yield return StartCoroutine(intro.PlayIntro(dreamCamera, tint, TintOf(Mathf.Max(0, index - 1))));
            yield return StartCoroutine(PlayerArrival(tint));
            player.frozen = false;
            CurrentMode = Mode.Playing;
        }
        else
        {
            CurrentMode = Mode.Playing;
            yield return screenFader.Fade(1f, 0f, startFadeDuration);
        }
    }

    // Switches the scene's fog and lighting to match a face
    void ApplyMood(int index, bool instant)
    {
        if (moodController == null) return;

        FaceMood mood = faces[index].GetComponent<FaceMood>();
        if (mood == null) return;

        if (instant) moodController.SnapToMood(mood);
        else moodController.SetMood(mood);
    }

    // Detects a double-click on the same face
    void HandleClick(Vector2 screenPos)
    {
        int index = GetFaceUnderMouse(screenPos);
        if (index < 0)
        {
            lastClickedFace = -1;
            return;
        }

        bool isDoubleClick = index == lastClickedFace &&
                             Time.unscaledTime - lastClickTime <= doubleClickTime;

        lastClickedFace = index;
        lastClickTime = Time.unscaledTime;

        if (isDoubleClick)
        {
            lastClickedFace = -1; // reset so a triple-click doesn't count twice
            TryEnterFace(index);
        }
    }

    // Fires a ray from the mouse and works out which face of the cube it hit
    int GetFaceUnderMouse(Vector2 screenPos)
    {
        Ray ray = dreamCamera.Cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return -1;
        if (!hit.transform.IsChildOf(transform)) return -1; // didn't hit the cube

        // The face whose "up" points most towards the hit spot is the one we clicked
        Vector3 dirFromCenter = (hit.point - transform.position).normalized;
        int best = 0;
        float bestDot = -2f;

        for (int i = 0; i < faces.Length; i++)
        {
            float dot = Vector3.Dot(faces[i].transform.up, dirFromCenter);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = i;
            }
        }
        return best;
    }

    // Enters a face if it's unlocked
    void TryEnterFace(int index)
    {
        if (CurrentMode != Mode.Overview) return;

        // Picking the face we were already on means going back to that exact spot
        if (index == CurrentFaceIndex && hasSavedSpot)
        {
            StartCoroutine(ReturnToSavedSpot());
            return;
        }

        if (faces[index].IsUnlocked)
            StartCoroutine(EnterFace(index));
        else
            Debug.Log("That dream is still locked.");
    }

    // Spins the cube relative to the camera's view, so dragging always feels natural
    void DragRotate(Vector2 delta)
    {
        Transform cam = dreamCamera.transform;
        transform.Rotate(cam.up, -delta.x * dragRotateSpeed, Space.World);
        transform.Rotate(cam.right, delta.y * dragRotateSpeed, Space.World);
    }

    // Finds the face whose "up" points most directly at the camera
    int GetFaceFacingCamera()
    {
        Vector3 camPos = dreamCamera.transform.position;
        int best = 0;
        float bestDot = -2f;

        for (int i = 0; i < faces.Length; i++)
        {
            Vector3 toCam = (camPos - faces[i].transform.position).normalized;
            float dot = Vector3.Dot(faces[i].transform.up, toCam);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = i;
            }
        }
        return best;
    }

    // Turns the cube so the chosen face is on top, then drops the player in
    IEnumerator EnterFace(int index)
    {
        CurrentMode = Mode.Transitioning;
        CurrentFaceIndex = index;
        CubeFace face = faces[index];

        // The backdrop fades and the level's fog rolls in as the cube turns
        backgroundGradient?.SetVisible(false);
        dreamerBackdrop?.SetVisible(false);
        moodController?.SetAwayFromLevel(false);

        // A fresh start on this face, so any saved spot is no longer valid
        hasSavedSpot = false;

        // A fresh layout for this visit, if that option is on
        if (regenerateOnEveryEntry)
        {
            faceSeeds[index] = Random.Range(int.MinValue, int.MaxValue);
            GenerateFace(index);
        }

        // A level with an entry animation starts empty, and builds itself once the cube has turned
        IFaceIntro intro = face.GetComponent<IFaceIntro>();
        if (intro != null) intro.HideForIntro();

        // Start blending the mood while the cube turns
        ApplyMood(index, false);

        // The rotation that puts this face flat on top, in the orientation you built it
        Quaternion start = transform.rotation;
        Quaternion target = Quaternion.Inverse(face.transform.localRotation);

        float duration = Mathf.Max(0.01f, faceTurnDuration);
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            transform.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.rotation = target;
        Physics.SyncTransforms();

        ApplyLevelRules(index);
        ApplyObjectives(index);

        // A new level starts with full hearts and full energy
        player.Vitals.RestoreAll();

        player.Spawn(face.spawnPoint);
        ApplyCameraStyle(index);
        dreamCamera.SetPlaying(player.transform);

        // The entry animation plays with the player hidden and unable to move. A crystal brings them in at the end.
        if (intro != null)
        {
            player.frozen = true;
            player.SetVisible(false);
            Color previousTint = TintOf(Mathf.Max(0, index - 1));
            yield return StartCoroutine(intro.PlayIntro(dreamCamera, TintOf(index), previousTint));
            yield return StartCoroutine(PlayerArrival(TintOf(index)));
            player.frozen = false;
        }

        CurrentMode = Mode.Playing;
    }

    // Called by the DreamDoor when the player walks into it
    public void CompleteCurrentLevel()
    {
        if (CurrentMode != Mode.Playing) return;

        // The finished level gets its crystal in the cube view
        faces[CurrentFaceIndex].SetCompleted(true, TintOf(CurrentFaceIndex));

        int next = CurrentFaceIndex + 1;

        if (next >= faces.Length)
        {
            Debug.Log("All dreams complete. The dreamer wakes!");
        }
        else if (next > highestUnlocked)
        {
            highestUnlocked = next;
            faces[next].SetUnlocked(true); // Lift the fog on the next face
        }

        GoToCubeView();
    }
}
