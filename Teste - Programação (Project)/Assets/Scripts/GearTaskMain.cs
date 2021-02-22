using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GearTaskMain : MonoBehaviour
{
    #region Private Control Variables
    // Transform Control Variables
    private readonly Quaternion worldGearsRotation_ = new Quaternion() { eulerAngles = new Vector3(0f, 0f, 20f) };
    private Vector2 uiStandardSize_ = new Vector2();
    private Vector2 uiToWorldSize_ = new Vector2();

    // Input and Behaviour Control Variables
    private RotationMode rotationMode_ = RotationMode.None;
    private int currentGear_ = -1;
    private bool lock_ = false;

    // Countdown Timers
    private Timer timer_ = new Timer()
    {
        movement = 0f,
        rotation = 0f,
        scale = 0f
    };
    #endregion

    #region Private Allocations
    private Camera camera_;
    private CanvasScaler canvasScaler_;
    #endregion

    #region Public Control Variables
    [Header("Baloon Texts")]
    [Space]
    [Header("TASK CONTROL VARIABLES")]
    [Tooltip("Default balloon text.")]
    [TextArea(3, 10)] public string textDefault;
    [Tooltip("Completed task balloon text.")]
    [TextArea(3, 10)] public string textCompleted;

    [Header("Completed Task Variables")]
    [Space]
    [Tooltip("Speed in degrees per second.")]
    public float constantRotationSpeed = 270f;
    [Tooltip("Waiting time to reset after completion.")]
    public float completedToResetWaitTime = 0.25f;

    [Header("Timers Setup")]
    [Space]
    [Tooltip("Behaviour smoothing time.")]
    public Timer smoothTimer = new Timer()
    {
        movement = 0.4f,
        rotation = 0.2f,
        scale = 0.3f
    };
    #endregion

    #region Public Allocations
    [Header("HUD Elements")]
    [Space]
    [Header("ALLOCATIONS")]
    [Space(20)]
    public Text balloon;
    public RectTransform inventory;
    public RectTransform[] slots;

    [Header("World Elements")]
    [Space]
    public CircleCollider2D[] gearPlacements;

    [Header("All Gears")]
    [Space]
    public GearEntity[] gears = new GearEntity[5];
    #endregion

    #region Structures
    [System.Serializable]
    public struct Timer
    {
        public float movement;
        public float rotation;
        public float scale;
    }

    [System.Serializable]
    public struct GearEntity
    {
        // The two gears that composes the entity
        public SpriteRenderer world;
        public RectTransform ui;

        // Information on gears locations
        public GearLocationInfo origin;
        public GearLocationInfo destiny;

        // Behaviour Control Variables
        [HideInInspector] public bool moving;
        [HideInInspector] public GearLocation scaling;

        // Variables to use in Lerps
        [HideInInspector] public Vector3 currentPosition;
        [HideInInspector] public Quaternion currentRotation;
        [HideInInspector] public Vector3 currentScale;

        /// <summary>
        /// Change the visibility between the two gears
        /// </summary>
        /// <param name="inventoryGear"> Visible gear should be the UI one? </param>
        public void VisibleGear(bool inventoryGear)
        {
            world.gameObject.SetActive(!inventoryGear);
            ui.gameObject.SetActive(inventoryGear);
        }

        /// <summary>
        /// This prevents the gears from passing behind any non moving gear.
        /// </summary>
        public void PutAhead()
        {
            world.sortingOrder = 1;
            ui.SetAsLastSibling();
        }

        /// <summary>
        /// Sets everything up for the gear to start moving.
        /// </summary>
        public void StartMoving()
        {
            PutAhead();

            currentPosition = world.transform.position;
            moving = true;

            Instance.SetMovementTimer();
        }

        /// <summary>
        /// Sets everything up for the gear to start scaling.
        /// </summary>
        /// <param name="nextSize"> Go to UI size or world size? </param>
        public void StartScaling(GearLocation nextSize)
        {
            // Scale Setup
            currentScale = ui.sizeDelta;
            scaling = nextSize;

            // Set the scaling timer
            Instance.SetScalingTimer();
        }
    }

    public struct GearLocationInfo
    {
        public GearLocation location;
        public int index;

        public void Clear()
        {
            location = GearLocation.Nowhere;
            index = -1;
        }
    }
    #endregion

    #region Enums
    public enum GearLocation
    {
        Nowhere,
        WorldPlacement,
        SlotUI
    }

    private enum RotationMode
    {
        None,
        TaskCompleted,
        BackToDefault
    }
    #endregion

    #region Reset Task
    // Method called by the button
    public void ResetButton() => StartCoroutine(Reseting());
    private IEnumerator Reseting()
    {
        // Prevents from executing while there's something moving
        yield return new WaitWhile(() => lock_);

        List<int> freeSlots = new List<int>();

        // Check if it's viable to reset
        bool canReset = false;
        for (int i = 0; i < gears.Length; i++)
        {
            freeSlots.Add(i);

            if (gears[i].origin.location == GearLocation.WorldPlacement)
            {
                canReset = true;
            }
        }

        // If it isn't, just leave, but, otherwise, if it indeed is, then lock the inputs while resetting
        if (!canReset) yield break;
        lock_ = true;

        // Check if the gears are rotating and, if they do, then revert the rotation before resetting the position
        if (rotationMode_ != RotationMode.None)
        {
            rotationMode_ = RotationMode.BackToDefault;
            SetRotationTimer();

            for (int i = 0; i < gears.Length; i++)
            {
                gears[i].currentRotation = gears[i].world.transform.rotation;
            }

            yield return new WaitUntil(() => rotationMode_ == RotationMode.None);
            yield return new WaitForSeconds(completedToResetWaitTime);
        }

        // Get the free slots and the gears on the world
        List<int> placedGears = new List<int>();
        for (int i = 0; i < gears.Length; i++)
        {
            if (gears[i].origin.location == GearLocation.SlotUI) freeSlots.Remove(gears[i].origin.index);
            else placedGears.Add(i);
        }

        // Puts each world gear in a random free slot
        do
        {
            int g = placedGears[Random.Range(0, placedGears.Count)];

            gears[g].destiny.location = GearLocation.SlotUI;
            gears[g].destiny.index = freeSlots[Random.Range(0, freeSlots.Count)];

            placedGears.Remove(g);
            freeSlots.Remove(gears[g].destiny.index);

            gears[g].StartScaling(GearLocation.SlotUI);
            gears[g].StartMoving();

            gears[g].VisibleGear(true);

        } while (freeSlots.Count > 0);

        // Revert the balloon text to its default
        balloon.text = textDefault;

        yield break;
    }
    #endregion

    #region Singleton
    public static GearTaskMain Instance { get; private set; }
    private void Awake() => Instance = this;
    #endregion

    #region MonoBehaviour Methods
    void Start()
    {
        // Defines the default text to the balloon
        balloon.text = textDefault;

        // Get the private allocations
        camera_ = FindObjectOfType<Camera>();
        canvasScaler_ = GetComponent<CanvasScaler>();

        // Set up the gears
        for (int i = 0; i < gears.Length; i++)
        {
            gears[i].origin = new GearLocationInfo();
            gears[i].destiny = new GearLocationInfo();

            GearArrived(i, i, true);
        }

        // Determines the scaled UI gear size based on the world gear size and the screen size 
        GetUiToWorldSize();
    }

    void Update()
    {
        // Closes the application
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();

        // Gears Behaviour
        Rotate();
        if (timer_.scale > 0f) Scale();
        if (timer_.movement > 0f) Move();

        // Input Behaviour
        if (lock_) return;
        else
        {
            if (currentGear_ < 0) InputDown();
            else
            {
                InputMove();
                InputUp();
            }
        }
    }
    #endregion

    #region Get Input Methods
    private void InputDown()
    {
        // If there's no input, just leave
        if (!Input.GetMouseButtonDown(0)) return;

        // Gets if the input was inside somewhere, and if it wasn't, then leave
        GearLocation location = InputInsideSomewhere(Input.mousePosition, out int index);
        if (location == GearLocation.Nowhere) return;

        // Gets if the input was on top of some gear
        currentGear_ = GearInsideLocation(location, index);
        if (currentGear_ >= 0)
        {
            // Sets the gear to move following the input
            gears[currentGear_].PutAhead();
            gears[currentGear_].StartScaling(GearLocation.WorldPlacement);

            // If there's a gear getting out of place, then the task can't be in completed state
            balloon.text = textDefault;
        }
    }

    private void InputMove()
    {
        GearEntity gear = gears[currentGear_];

        // Get the gear positions in Vector2 so the z coordinate will always be 0
        Vector2 uiPosition = Input.mousePosition;
        Vector2 worldPosition = camera_.ScreenToWorldPoint(uiPosition);

        // Set the gear positions
        gear.ui.position = uiPosition;
        gear.world.transform.position = worldPosition;

        // Changes the visible gear whether the input is in or outside the inventory
        gear.VisibleGear(RectTransformUtility.RectangleContainsScreenPoint(inventory, uiPosition));
    }

    private void InputUp()
    {
        // If there's no input, just leave
        if (!Input.GetMouseButtonUp(0)) return;

        GearEntity gear = gears[currentGear_];

        // Gets if the input was inside of somewhere. If not, the gear returns to its origin, else it gets the coordinates of the new destiny
        GearLocation location = InputInsideSomewhere(Input.mousePosition, out int index);
        if (location == GearLocation.Nowhere) gear.destiny = gear.origin;
        else
        {
            gear.destiny.location = location;
            gear.destiny.index = index;

            // Checks if there was another gear at the new destiny
            int g = GearInsideLocation(location, index);

            // If it was, then determines the destiny of this another gear to be the origin of the moving gear
            if (g >= 0)
            {
                gears[g].destiny = gear.origin;
                SetGear(g);
            }
        }

        gears[currentGear_] = gear;
        SetGear(currentGear_);

        // Empties the holder and lock the inputs
        currentGear_ = -1;
        lock_ = true;
    }
    #endregion

    #region Gears Behaviour
    // All gears can be moved
    private void Move()
    {
        // Decrements movement timer
        timer_.movement -= Time.deltaTime;

        // If movement timer has run out, sets all moving gears to get in their destiny place and unlock the inputs
        if (timer_.movement <= 0f)
        {
            timer_.movement = 0f;

            for (int i = 0; i < gears.Length; i++)
            {
                if (gears[i].moving)
                {
                    GearArrived(i, gears[i].destiny.index, gears[i].destiny.location == GearLocation.SlotUI);
                }
            }

            lock_ = false;
        }
        else
        {
            // Gets the percentage to use on Lerp and declare the destiny position variable
            float percentage = Mathf.InverseLerp(smoothTimer.movement, 0f, timer_.movement);
            Vector3 destinyPos;

            for (int i = 0; i < gears.Length; i++)
            {
                // Only interfere with the position of a gear that's moving
                if (gears[i].moving)
                {
                    // Gets the position based on the destiny location
                    if (gears[i].destiny.location == GearLocation.WorldPlacement) destinyPos = gearPlacements[gears[i].destiny.index].transform.position;
                    else destinyPos = camera_.ScreenToWorldPoint(slots[gears[i].destiny.index].position);
                    destinyPos.z = 0f;

                    // Move first the world gear
                    gears[i].world.transform.position = Vector3.Lerp(gears[i].currentPosition, destinyPos, percentage);

                    // After that, the destiny position variable is free to be used to get the UI gear position
                    destinyPos = camera_.WorldToScreenPoint(gears[i].world.transform.position);
                    destinyPos.z = 0f;

                    // And, finally, moves the UI gear
                    gears[i].ui.position = destinyPos;
                }
            }
        }
    }

    // Only UI gears go through scaling process
    private void Scale()
    {
        // Decrements scale timer
        timer_.scale -= Time.deltaTime;

        // If scale timer has run out, sets all scaling gears to their determinated destiny sizes
        if (timer_.scale <= 0f)
        {
            timer_.scale = 0f;

            for (int i = 0; i < gears.Length; i++)
            {
                switch (gears[i].scaling)
                {
                    case GearLocation.WorldPlacement:
                        gears[i].ui.sizeDelta = uiToWorldSize_;
                        break;
                    case GearLocation.SlotUI:
                        gears[i].ui.sizeDelta = uiStandardSize_;
                        break;
                    default:
                        break;
                }

                gears[i].scaling = GearLocation.Nowhere;
            }
        }
        else
        {
            // Gets the percentage to use on Lerp and declare the new scale variable
            float percentage = Mathf.InverseLerp(smoothTimer.scale, 0f, timer_.scale);
            Vector2 newScale;

            // Determine if the new scale'll be world size, UI size or current size
            for (int i = 0; i < gears.Length; i++)
            {
                switch (gears[i].scaling)
                {
                    case GearLocation.WorldPlacement:
                        newScale = uiToWorldSize_;
                        break;
                    case GearLocation.SlotUI:
                        newScale = uiStandardSize_;
                        break;
                    default:
                        newScale = gears[i].currentScale;
                        break;
                }

                // Scale through lerp
                gears[i].ui.sizeDelta = Vector2.Lerp(gears[i].currentScale, newScale, percentage);
            }
        }
    }

    // Only world gears can be rotated
    private void Rotate()
    {
        // Checks in which rotation mode the game currently is
        switch (rotationMode_)
        {
            // Case: No rotation mode, just leave
            case RotationMode.None:
                return;

            // Case: Task completed mode, then rotate indefinitely on a constant speed
            case RotationMode.TaskCompleted:

                // Verifies if all gears are still in place, if they are, then keep spinning, else, change to reverse rotation mode and sets the rotation timer
                if (AllGearsPositioned())
                {
                    for (int i = 0; i < gears.Length; i++)
                    {
                        Transform gear = gears[i].world.transform;

                        Vector3 eulers = gear.rotation.eulerAngles;
                        eulers.z += (gears[i].origin.index % 2 == 0f ? -constantRotationSpeed : constantRotationSpeed) * Time.deltaTime;

                        gear.rotation = new Quaternion() { eulerAngles = eulers };
                    }
                }
                else
                {
                    for (int i = 0; i < gears.Length; i++)
                    {
                        gears[i].currentRotation = gears[i].world.transform.rotation;
                    }

                    rotationMode_ = RotationMode.BackToDefault;
                    SetRotationTimer();
                }
                break;

            // Case: Reverse rotation mode, then get gears rotation back to default on the smooth rotation time
            case RotationMode.BackToDefault:

                // Decrements rotation timer
                timer_.rotation -= Time.deltaTime;

                // If rotation timer has run out, sets all gears to the default rotation
                if (timer_.rotation <= 0f)
                {
                    timer_.rotation = 0f;

                    for (int i = 0; i < gears.Length; i++)
                    {
                        gears[i].world.transform.rotation = worldGearsRotation_;
                    }

                    rotationMode_ = RotationMode.None;
                }
                else
                {
                    // Gets the percentage to use on Lerp
                    float percentage = Mathf.InverseLerp(smoothTimer.rotation, 0f, timer_.rotation);

                    // Get new euler angles using the Lerp and set a new rotation using them
                    for (int i = 0; i < gears.Length; i++)
                    {
                        Vector3 eulers = Vector3.Lerp(gears[i].currentRotation.eulerAngles, worldGearsRotation_.eulerAngles, percentage);

                        gears[i].world.transform.rotation = new Quaternion() { eulerAngles = eulers };
                    }
                }
                break;
        }
    }
    #endregion

    #region Gears Control
    /// <summary>
    /// Set the gear up to start moving and scaling.
    /// </summary>
    /// <param name="index"> The index of the gear to be setted up. </param>
    private void SetGear(int index)
    {
        gears[index].StartMoving();
        gears[index].StartScaling(gears[index].destiny.location);

        if (gears[index].destiny.location == GearLocation.SlotUI) gears[index].VisibleGear(true);
    }

    /// <summary>
    /// configures the gear using the destination information.
    /// </summary>
    /// <param name="gearIndex"> Which gear? </param>
    /// <param name="destinyIndex"> The index of the destination. </param>
    /// <param name="inInventory"> Is the destiny an inventory slot (true) or a gear placement (false)? </param>
    private void GearArrived(int gearIndex, int destinyIndex, bool inInventory)
    {
        GearEntity gear = gears[gearIndex];

        // Sets the new origin information up
        gear.origin.location = inInventory ? GearLocation.SlotUI : GearLocation.WorldPlacement;
        gear.origin.index = destinyIndex;

        // Clears up the destiny information
        gear.destiny.Clear();

        Vector2 position;

        // Positions the gear and applies the default settings from where it is
        if (inInventory)
        {
            gear.ui.position = slots[destinyIndex].position;
            position = camera_.ScreenToWorldPoint(gear.ui.position);

            gear.world.transform.position = position;
            gear.world.transform.rotation = worldGearsRotation_;
        }
        else
        {
            gear.world.transform.position = gearPlacements[destinyIndex].transform.position;
            position = camera_.WorldToScreenPoint(gear.world.transform.position);

            gear.ui.position = position;
            gear.ui.sizeDelta = uiToWorldSize_;
        }

        // Returns the world gear to the default sorting order, switchs the visibilities and stops the gear. At last, checks if all gears are in position
        gear.world.sortingOrder = 0;
        gear.currentScale = gear.ui.sizeDelta;

        gear.VisibleGear(inInventory);
        gear.moving = false;

        gears[gearIndex] = gear;
        AllGearsPositioned();
    }

    /// <summary>
    /// checks whether the input is inside a predetermined place or not.
    /// </summary>
    /// <param name="inputPoint"> Input position. </param>
    /// <param name="index"> The index from the place where the input is. </param>
    /// <returns> Returns whether the input is on top of an inventory slot, a gear placement or nowhere. </returns>
    private GearLocation InputInsideSomewhere(Vector2 inputPoint, out int index)
    {
        for (int i = 0; i < gearPlacements.Length; i++)
        {
            index = i;

            if (Physics2D.OverlapPoint(camera_.ScreenToWorldPoint(inputPoint)) == gearPlacements[i]) return GearLocation.WorldPlacement;
            else if (RectTransformUtility.RectangleContainsScreenPoint(slots[i], inputPoint)) return GearLocation.SlotUI;
        }

        index = -1;
        return GearLocation.Nowhere;
    }

    /// <summary>
    /// Checks if there's some gear inside of the given location.
    /// </summary>
    /// <param name="location"> The location to check. </param>
    /// <param name="locationIndex"> The index of the location. </param>
    /// <returns> Returns the index of the gear that's inside that location, returns -1 if there's not. </returns>
    private int GearInsideLocation(GearLocation location, int locationIndex)
    {
        for (int i = 0; i < gears.Length; i++)
        {
            if (gears[i].origin.location == location && gears[i].origin.index == locationIndex) return i;
        }

        return -1;
    }

    /// <summary>
    /// Checks if all the gears are putted up in the gear placements.
    /// </summary>
    /// <returns> Returns false if even one are being moved or isn't in place. </returns>
    private bool AllGearsPositioned()
    {
        if (currentGear_ >= 0) return false;

        for (int i = 0; i < gears.Length; i++)
        {
            if (gears[i].origin.location != GearLocation.WorldPlacement || gears[i].destiny.location == GearLocation.SlotUI) return false;
        }

        // If all is setted up, then change the balloon text and the rotation mode.
        balloon.text = textCompleted;
        rotationMode_ = RotationMode.TaskCompleted;
        return true;
    }
    #endregion

    #region Set Timers
    // Methods to simplify the timers setup

    public void SetMovementTimer() => timer_.movement = smoothTimer.movement;
    public void SetScalingTimer() => timer_.scale = smoothTimer.scale;
    public void SetRotationTimer() => timer_.rotation = smoothTimer.rotation;
    #endregion

    #region Utilities
    /// <summary>
    /// Gets the scale factor from the canvas scaler and the screen size.
    /// </summary>
    /// <returns> Returns the scale factor to use to get the exact position of something inside this canvas. </returns>
    private float ScreenScaleFactor()
    {
        if (canvasScaler_.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            switch (canvasScaler_.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:
                    return Mathf.Lerp(Screen.width, Screen.height, canvasScaler_.matchWidthOrHeight) / Mathf.Lerp(canvasScaler_.referenceResolution.x, canvasScaler_.referenceResolution.y, canvasScaler_.matchWidthOrHeight);
                case CanvasScaler.ScreenMatchMode.Expand:
                    return Screen.height / canvasScaler_.referenceResolution.y;
                case CanvasScaler.ScreenMatchMode.Shrink:
                    return Screen.width / canvasScaler_.referenceResolution.x;
                default:
                    break;
            }
        }

        return 1f;
    }

    /// <summary>
    /// Method used to get the UI gear scaling size to be the same size in camera of the world gear.
    /// </summary>
    private void GetUiToWorldSize()
    {
        RectTransform uiGear = gears[0].ui;
        uiStandardSize_ = uiGear.sizeDelta;

        float worldSize = gearPlacements[0].radius * 2f;

        Vector3 up = new Vector3()
        {
            x = uiGear.position.x,
            y = uiGear.position.y + uiGear.rect.height / 2f,
            z = 0f
        };
        up *= ScreenScaleFactor();

        Vector3 down = new Vector3()
        {
            x = uiGear.position.x,
            y = uiGear.position.y - uiGear.rect.height / 2f,
            z = 0f
        };
        down *= ScreenScaleFactor();

        float uiSize = Vector3.Distance(camera_.ScreenToWorldPoint(up), camera_.ScreenToWorldPoint(down));

        uiToWorldSize_ = uiStandardSize_ * (worldSize / uiSize);
    }
    #endregion
}
