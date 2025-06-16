using System.Collections.Generic;
using UnityEngine;

public class EndlessPath : MonoBehaviour
{
    public GameObject straightBridgePrefab;
    public GameObject L_CornerBridgePrefab; // 90-degree left turn
    public GameObject R_CornerBridgePrefab; // 90-degree right turn
    public GameObject Half_L_LaneObstaclePrefab; // Path missing right half, go left
    public GameObject Half_R_LaneObstaclePrefab; // Path missing left half, go right

    private List<GameObject> activeSegments = new List<GameObject>();

    private Vector3 currentSpawnPosition = Vector3.zero;
    private Quaternion currentSpawnRotation = Quaternion.identity;

    [Tooltip("Length of a single straight segment in Z-axis.")]
    public float segmentLength = 6.0f; // Each ground piece is 6 units long in Z

    [Tooltip("Target number of segments to keep active in view and ahead of the player.")]
    public int targetSegmentsInView = 25; // Aim for more segments to avoid popping

    [Tooltip("Minimum number of straight segments to spawn after a turn or lane change.")]
    public int minStraightSegmentsAfterTurn = 4;
    private int straightSegmentsCount = 0;

    [Tooltip("Z-offset for the next segment's position when placing a 90-degree corner.")]
    public float corner90_DepthOffset = 1.5f;
    [Tooltip("X/Y offset for the next segment's position when placing a 90-degree corner.")]
    public float corner90_WidthOffset = 6f;

    [Tooltip("Number of consecutive half-lane segments that can spawn in a sequence.")]
    public int sequenceLength = 3;
    private SegmentType currentSequenceType = SegmentType.Straight;
    private int segmentsInSequenceCount = 0;

    [Tooltip("Lateral offset for half-lane segments to align correctly.")]
    public float halfLaneSideAlignmentOffset = 0.5f;

    private SegmentType lastSpawnedLaneType = SegmentType.Straight;

    [Tooltip("The player's Transform. Assign in Inspector or ensure it's tagged 'Player'.")]
    public Transform playerTransform; // Assign your player's transform here in the Inspector

    [Tooltip("How far ahead of the player (in player's forward direction) the last segment must be before a new segment is spawned.")]
    public float spawnTriggerDistance = 80.0f; // Increased for a larger buffer

    [Tooltip("Small buffer beyond the segment's end (in segment's local Z) to ensure full passage before despawn.")]
    public float despawnBuffer = 0.5f; // Small buffer beyond the segment's end to ensure full passage

    // Variable to track initial straight spawns
    private int initialStraightSpawnCounter = 0;
    private const int MAX_INITIAL_STRAIGHT_SPAWNS = 5; // Define the fixed limit

    // This value is critical for delaying destruction out of sight.
    // It determines how many *segments* behind the player's immediate visual range
    // the oldest segment will be before it's considered for despawn.
    [Tooltip("Number of segments to keep visible behind the player's camera before despawning.")]
    public int extraSegmentsBehindCamera = 3; // Keep 3 extra segments behind camera's trail

    enum SegmentType
    {
        Straight,
        L_Corner,
        R_Corner,
        Half_L_Lane,
        Half_R_Lane
    }

    void Start()
    {
        // Safety check: Find player if not assigned in Inspector
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("Player GameObject with tag 'Player' not found! Assign playerTransform in the Inspector or ensure player is tagged.");
                enabled = false; // Disable script if no player is found
                return;
            }
        }

        // --- Initial Spawn Logic ---
        // Spawn enough initial segments to cover the targetSegmentsInView,
        // prioritizing the fixed initial straight segments.
        // We ensure enough segments are spawned initially to fill the view AND the "behind player" buffer.
        // Calculate the effective number of segments needed to be *behind* the player for the camera.
        // The camera is at target.position - target.forward * trailDistance.
        // To be safe, let's ensure enough segments are spawned to cover the camera's trail distance
        // plus some extra segments behind that.
        
        // This calculates the minimum number of segments that should *always* be kept
        // between the player's current position and the despawn point.
        // player on segment 0
        // camera on segment -1 (behind player)
        // first segment in list might be segment -5 (5 segments behind player)
        // We want to despawn when activeSegments[0] is sufficiently behind the camera.

        // Let's refine the initial spawn count to be simpler and ensure view is filled.
        int totalInitialSegmentsToSpawn = targetSegmentsInView + extraSegmentsBehindCamera + MAX_INITIAL_STRAIGHT_SPAWNS;

        for (int i = 0; i < totalInitialSegmentsToSpawn; i++)
        {
            if (i < MAX_INITIAL_STRAIGHT_SPAWNS)
            {
                SpawnSegment(SegmentType.Straight);
            }
            else
            {
                SpawnNextSegment();
            }
            initialStraightSpawnCounter++; 
        }
        
        straightSegmentsCount = MAX_INITIAL_STRAIGHT_SPAWNS;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            return;
        }

        // --- Despawn Logic: Destroy segments when they are sufficiently behind the player's camera ---
        // The camera is 'trailDistance' behind the player. We want the despawn to occur 'extraSegmentsBehindCamera'
        // segments behind where the camera is.
        
        // Calculate the position of the camera relative to the player.
        // This assumes the CameraMovement script is correctly updating the camera's position.
        // We need to know the camera's 'trailDistance' from the CameraMovement script.
        // For simplicity and robustness, let's consider the player's position and the `extraSegmentsBehindCamera` value.
        // The `segmentsToKeepBehindPlayer` was a bit ambiguous as to what it meant relative to the camera vs. player.
        // This new `extraSegmentsBehindCamera` is clearer.

        // The point where we want to consider despawning is `extraSegmentsBehindCamera` segments
        // behind the player's *current* position.
        float despawnPointRelativeToPlayer = (extraSegmentsBehindCamera * segmentLength) + despawnBuffer;

        while (activeSegments.Count > 0) 
        {
            GameObject firstSegment = activeSegments[0];
            
            // Calculate the vector from the segment's origin to the player's origin
            // Then project it onto the player's current forward direction.
            // A positive value means the segment is ahead, negative means behind.
            Vector3 vectorSegmentToPlayer = playerTransform.position - firstSegment.transform.position;
            float distanceOfSegmentBehindPlayer = Vector3.Dot(vectorSegmentToPlayer, playerTransform.forward);

            // If the segment is behind the player by more than the calculated despawn distance
            if (distanceOfSegmentBehindPlayer > despawnPointRelativeToPlayer) 
            {
                Destroy(firstSegment);
                activeSegments.RemoveAt(0);
                
                // Immediately attempt to spawn after despawn to maintain the buffer
                if (activeSegments.Count < targetSegmentsInView + extraSegmentsBehindCamera + MAX_INITIAL_STRAIGHT_SPAWNS) // Adjusted for new variable
                {
                    SpawnNextSegment();
                }
            }
            else
            {
                // If the first segment is not yet passed, no subsequent segments will be either.
                break;
            }
        }

        // --- Spawn Logic: Continuously ensure enough segments ahead of the player ---
        // Spawn if the number of active segments falls below the target (ahead + behind camera buffer) AND
        // the player is close to the end of the last spawned segment.
        if (activeSegments.Count < targetSegmentsInView + extraSegmentsBehindCamera + MAX_INITIAL_STRAIGHT_SPAWNS) // Adjusted for new variable
        {
            // Removed the `activeSegments.Count == 0 && initialStraightSpawnCounter >= MAX_INITIAL_STRAIGHT_SPAWNS`
            // condition here as the `Start()` method ensures enough segments are always present initially,
            // and the primary `if` condition with `SpawnNextSegment()` ensures continuous spawning.
            
            if (activeSegments.Count > 0) 
            {
                GameObject lastSegment = activeSegments[activeSegments.Count - 1];
                
                // Check if the player is approaching the end of the last segment in the list
                Vector3 playerToLastSegment = playerTransform.position - lastSegment.transform.position;
                float distanceAlongLastSegmentForward = Vector3.Dot(playerToLastSegment, lastSegment.transform.forward);

                if (distanceAlongLastSegmentForward >= (segmentLength - spawnTriggerDistance))
                {
                    SpawnNextSegment();
                }
            }
        }
    }

    void SpawnNextSegment()
    {
        SegmentType nextType;

        // Priority 1: Force straight for initial spawns (handled primarily in Start, but kept here for robustness)
        if (initialStraightSpawnCounter < MAX_INITIAL_STRAIGHT_SPAWNS)
        {
            nextType = SegmentType.Straight;
            initialStraightSpawnCounter++; 
        }
        // Priority 2: Continue an ongoing sequence (e.g., half-lane obstacles)
        else if (segmentsInSequenceCount > 0 && segmentsInSequenceCount < sequenceLength)
        {
            nextType = currentSequenceType;
        }
        // Priority 3: Ensure minimum straight segments after a turn/lane change
        else if (straightSegmentsCount < minStraightSegmentsAfterTurn)
        {
            nextType = SegmentType.Straight;
        }
        // Priority 4: Randomly select a new segment type
        else
        {
            List<SegmentType> possibleNextTypes = new List<SegmentType>
            {
                SegmentType.Straight, 
                SegmentType.Straight, 
                SegmentType.Straight, // Increased likelihood for straight segments
                SegmentType.L_Corner,
                SegmentType.R_Corner,
                SegmentType.Half_L_Lane,
                SegmentType.Half_R_Lane
            };

            // Prevent immediate opposite lane changes for better playability
            if (lastSpawnedLaneType == SegmentType.Half_L_Lane)
            {
                possibleNextTypes.Remove(SegmentType.Half_R_Lane);
            }
            else if (lastSpawnedLaneType == SegmentType.Half_R_Lane)
            {
                possibleNextTypes.Remove(SegmentType.Half_L_Lane);
            }

            int randomChoiceIndex = Random.Range(0, possibleNextTypes.Count);
            nextType = possibleNextTypes[randomChoiceIndex];

            // Reset or start new sequence/lane tracking based on the chosen type
            if (nextType == SegmentType.Half_L_Lane || nextType == SegmentType.Half_R_Lane)
            {
                currentSequenceType = nextType;
                segmentsInSequenceCount = 0; // Start the sequence count for the new type
            }
            else
            {
                currentSequenceType = SegmentType.Straight; // Reset for non-sequence types
                segmentsInSequenceCount = 0;
                lastSpawnedLaneType = SegmentType.Straight; // Reset last lane type
            }
        }
        SpawnSegment(nextType);
    }

    void SpawnSegment(SegmentType type)
    {
        GameObject prefabToInstantiate = null;
        Vector3 spawnAtPosition = currentSpawnPosition;
        Quaternion spawnAtRotation = currentSpawnRotation;

        Vector3 nextSegmentPosition = currentSpawnPosition;
        Quaternion nextSegmentRotation = currentSpawnRotation;

        bool isRealTurnSegment = false;
        Vector3 instantiationOffset = Vector3.zero;

        // Determine prefab, next spawn position, and rotation based on segment type
        switch (type)
        {
            case SegmentType.Straight:
                prefabToInstantiate = straightBridgePrefab;
                nextSegmentPosition += currentSpawnRotation * Vector3.forward * segmentLength;
                straightSegmentsCount++;
                break;

            case SegmentType.L_Corner:
                prefabToInstantiate = L_CornerBridgePrefab;
                nextSegmentRotation *= Quaternion.Euler(0, -90, 0);
                nextSegmentPosition += currentSpawnRotation * Vector3.forward * corner90_DepthOffset;
                nextSegmentPosition += nextSegmentRotation * Vector3.forward * corner90_WidthOffset;
                isRealTurnSegment = true;
                break;

            case SegmentType.R_Corner:
                prefabToInstantiate = R_CornerBridgePrefab;
                nextSegmentRotation *= Quaternion.Euler(0, 90, 0);
                nextSegmentPosition += currentSpawnRotation * Vector3.forward * corner90_DepthOffset;
                nextSegmentPosition += nextSegmentRotation * Vector3.forward * corner90_WidthOffset;
                isRealTurnSegment = true;
                break;

            case SegmentType.Half_L_Lane:
                prefabToInstantiate = Half_L_LaneObstaclePrefab;
                nextSegmentPosition += currentSpawnRotation * Vector3.forward * segmentLength;
                straightSegmentsCount++;
                segmentsInSequenceCount++;
                instantiationOffset = currentSpawnRotation * Vector3.left * halfLaneSideAlignmentOffset;
                lastSpawnedLaneType = SegmentType.Half_L_Lane;
                break;

            case SegmentType.Half_R_Lane:
                prefabToInstantiate = Half_R_LaneObstaclePrefab;
                nextSegmentPosition += currentSpawnRotation * Vector3.forward * segmentLength;
                straightSegmentsCount++;
                segmentsInSequenceCount++;
                instantiationOffset = currentSpawnRotation * Vector3.right * halfLaneSideAlignmentOffset;
                lastSpawnedLaneType = SegmentType.Half_R_Lane;
                break;
        }

        // Reset straight segment count if a turn occurred
        if (isRealTurnSegment)
        {
            straightSegmentsCount = 0;
        }
        
        // Handle sequence length for lane changes
        if ((type == SegmentType.Half_L_Lane || type == SegmentType.Half_R_Lane) && segmentsInSequenceCount >= sequenceLength)
        {
            currentSequenceType = SegmentType.Straight;
            segmentsInSequenceCount = 0;
        }
        else if (type != SegmentType.Half_L_Lane && type != SegmentType.Half_R_Lane)
        {
            lastSpawnedLaneType = SegmentType.Straight; // Reset if we're not spawning a lane type
        }

        // Instantiate the segment if a valid prefab is assigned
        if (prefabToInstantiate != null)
        {
            GameObject newSegment = Instantiate(prefabToInstantiate, spawnAtPosition + instantiationOffset, spawnAtRotation);
            activeSegments.Add(newSegment);

            // Update global spawn state for the *next* segment
            currentSpawnPosition = nextSegmentPosition;
            currentSpawnRotation = nextSegmentRotation;
        }
        else
        {
            // This error will tell you exactly which prefab is missing
            Debug.LogError($"Prefab for segment type {type} is null! Please assign it in the Inspector. Cannot instantiate.");
        }
    }
}