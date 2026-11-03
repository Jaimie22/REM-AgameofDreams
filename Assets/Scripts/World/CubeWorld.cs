
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
    }

    void Start()
    {
        // Create a fader automatically if none was assigned
        if (screenFader == null)
            screenFader = gameObject.AddComponent<ScreenFader>();

        // The green / amber / red light showing how aware the hunter is, also created automatically
        if (detectionIndicator == null)
            detectionIndicator = gameObject.AddComponent<DetectionIndicator>();

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

        for (int i = 0; i < faces.Length; i++)
            faces[i].SetUnlocked(unlockAllFaces || i <= highestUnlocked);

        StartCoroutine(StartSequence(startIndex));
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

        // The game opens inside a level, not in the cube view
        backgroundGradient?.SetVisible(false);
        dreamerBackdrop?.SetVisible(false);
        moodController?.SetAwayFromLevel(false);

       
        ApplyLevelRules(index);
        ApplyObjectives(index);

        player.Spawn(face.spawnPoint);
        ApplyCameraStyle(index);
        dreamCamera.SetPlaying(player.transform);
        dreamCamera.SnapToTarget();
        ApplyMood(index, true);

        yield return null; // let one frame settle before revealing

        CurrentMode = Mode.Playing;
        yield return screenFader.Fade(1f, 0f, startFadeDuration);
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

        player.Spawn(face.spawnPoint);
        ApplyCameraStyle(index);
        dreamCamera.SetPlaying(player.transform);
        CurrentMode = Mode.Playing;
    }

    // Called by the DreamDoor when the player walks into it
    public void CompleteCurrentLevel()
    {
        if (CurrentMode != Mode.Playing) return;

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
