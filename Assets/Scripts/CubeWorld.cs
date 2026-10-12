using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// The "brain" of the game. Put this on the cube's root object (pivot at the cube's centre).
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
    [Tooltip("Optional. Handles fog and lighting changes between faces.")]
    public MoodController moodController;

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

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Create a fader automatically if none was assigned
        if (screenFader == null)
            screenFader = gameObject.AddComponent<ScreenFader>();

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

    // Builds (or rebuilds) one face's layout
    void GenerateFace(int index)
    {
        LevelGenerator generator = faces[index].GetComponent<LevelGenerator>();
        if (generator == null) return;

        generator.Generate(faceSeeds[index]);
        Physics.SyncTransforms(); // make sure the new colliders are in place
    }

    // Applies this level's gravity and jump height
    void ApplyLevelRules(int index)
    {
        LevelGenerator generator = faces[index].GetComponent<LevelGenerator>();
        if (generator == null || generator.theme == null) return;

        player.ApplyMovementSettings(generator.theme.gravity, generator.theme.jumpHeight);
    }

    void Update()
    {
        if (CurrentMode == Mode.Transitioning) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        // M toggles the map from anywhere
        if (kb != null && kb.mKey.wasPressedThisFrame)
        {
            ToggleMap();
            return;
        }

        if (mouse == null) return;
        float scroll = mouse.scroll.ReadValue().y;

        switch (CurrentMode)
        {
            case Mode.Playing:
                // Scroll down = out to the cube
                if (scroll < 0f)
                {
                    GoToCubeView();
                    return;
                }

                // Respawn if the player falls off the level
                CubeFace face = faces[CurrentFaceIndex];
                if (player.transform.position.y < face.transform.position.y - fallResetDistance)
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

    // --- Views ------------------------------------------------------

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
        CurrentMode = Mode.Map;
    }

    // Pull back to the whole cube, where it can be spun and faces picked
    public void GoToCubeView()
    {
        SaveCurrentSpot();
        player.Hide();
        dreamCamera.SetOverview();
        CurrentMode = Mode.Overview;
    }

    // Kept so other scripts calling the old name still work
    public void GoToOverview()
    {
        GoToCubeView();
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
        player.Spawn(worldPos, worldRot);
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

        ApplyLevelRules(index);
        player.Spawn(face.spawnPoint);
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
        player.Spawn(face.spawnPoint);
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