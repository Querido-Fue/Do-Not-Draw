using System;
using DoNotDraw.Interaction;
using DoNotDraw.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DoNotDraw.Narrative.Editor
{
    internal sealed class DetailedRoomSetRefs
    {
        public GameObject FirstRoomSet;
        public GameObject SecondRoomSet;
        public HorrorDoorInteractable SecondDoor;
        public HorrorDoorInteractable StoryDoor;
        public GameObject SecondDoorCover;
        public CardDeckPresenter SecondPresenter;
        public CardDeckInteraction SecondInteraction;
        public Light FirstLamp;
        public Light SecondLamp;
        public Renderer FirstRoomSurfaceRenderer;
        public Renderer SecondRoomSurfaceRenderer;
        public Light MoonLight;
        public Light RearRimLight;
        public Light SilhouetteBacklight;
        public Light ExitLight;
        public NarrativeZoneTrigger SecondRoomZone;
        public NarrativeZoneTrigger ReturnZone;
        public NarrativeZoneTrigger EndingZone;
        public Transform PlayerStartMarker;
        public Transform SecondRoomPlayerMarker;
        public Transform FirstRearRimAnchor;
        public Transform SecondRearRimAnchor;
        public Transform WindowGazeTarget;
        public GameObject WindowVision;
        public Transform Threat;
        public Transform ThreatStart;
        public Transform ThreatEnd;
        public Transform FirstClockHand;
        public Transform SecondClockHand;
        public Transform ShadowCaster;
        public GameObject EndingPortraitSilhouette;
    }

    internal static class DetailedRoomSetFactory
    {
        private const float RoomWidth = 6f;
        private const float RoomDepth = 4.8f;
        private const float RoomHeight = 3f;
        private const float RoomSeparation = 0.2f;
        private const float SecondRoomCenterZ = RoomDepth + RoomSeparation;
        private const float NorthWallZ = RoomDepth * 0.5f;
        private const float SecondNorthWallZ = SecondRoomCenterZ + RoomDepth * 0.5f;
        private const float FirstDoorX = -2f;
        private const float WindowX = 0f;
        private const float SecondDoorX = 2f;
        private const float DoorWidth = 1f;
        private const float DoorHeight = 2.3f;
        private const string PreplacedCardTexturePath =
            "Assets/Art/Card/Card3_DoNotOpenTheSecondDoor.png";
        private const string PreplacedCardMaterialPath =
            "Assets/DoNotDraw/Materials/Final/PreplacedCardFour.mat";
        private const string ThreatEntityPrefabPath =
            "Assets/ExternalModels/BackroomsEntity/BackroomsEntity.prefab";
        private const string BackroomsSurfacePrefabPath =
            "Assets/Asset/BackroomsLikeAsset/prefab/Tiles/Tiles_01_Fill.prefab";
        private const float BackroomsSurfaceWidth = 4f;
        private const float BackroomsSurfaceHeight = 3.75f;
        private const float BackroomsSurfaceDepth = 4f;

        public static DetailedRoomSetRefs Build(
            Transform parent,
            GameObject originalDesk,
            Transform player,
            Material wall,
            Material floor,
            Material ceiling,
            Material ceilingGrid,
            Material wallTrim,
            Material doorMaterial,
            Material wood,
            Material brass,
            Material nickel,
            Material windowMaterial,
            Material curtainMaterial,
            Material silhouetteMaterial,
            Material visionMaterial,
            Material whiteLightMaterial,
            AudioClip doorCreak,
            AudioClip doorSlam)
        {
            if (parent == null || originalDesk == null || player == null)
            {
                throw new ArgumentNullException(nameof(parent), "Detailed room construction requires a parent, desk, and player.");
            }

            DetailedRoomSetRefs refs = new DetailedRoomSetRefs();
            RemoveChildrenNamed(originalDesk.transform, "Back Apron");
            refs.FirstRoomSet = new GameObject("First Room - 6x4.8 Backrooms");
            refs.FirstRoomSet.transform.SetParent(parent, false);
            refs.SecondRoomSet = new GameObject("Second Room - Mirrored 6x4.8 Backrooms");
            refs.SecondRoomSet.transform.SetParent(parent, false);

            CharacterController playerController = player.GetComponent<CharacterController>();
            if (playerController != null)
            {
                playerController.radius = 0.4f;
                playerController.skinWidth = 0.02f;
            }

            refs.FirstRoomSurfaceRenderer = BuildFirstRoomShell(
                refs.FirstRoomSet.transform,
                wall,
                floor,
                ceiling,
                ceilingGrid,
                wallTrim);
            refs.SecondRoomSurfaceRenderer = BuildSecondRoomShell(
                refs.SecondRoomSet.transform,
                wall,
                floor,
                ceiling,
                ceilingGrid,
                wallTrim);

            originalDesk.transform.position = Vector3.zero;
            originalDesk.transform.rotation = Quaternion.identity;
            originalDesk.transform.localScale = Vector3.one;
            refs.FirstLamp = BuildFluorescentCeilingRig(
                "First Room Fluorescent Ceiling Rig",
                refs.FirstRoomSet.transform,
                0f,
                24f);

            GameObject secondDesk = UnityEngine.Object.Instantiate(originalDesk, refs.SecondRoomSet.transform);
            secondDesk.name = "Desk - Second Room";
            secondDesk.transform.position = new Vector3(0f, 0f, SecondRoomCenterZ);
            GameObject deckObject = FindChildRecursive(secondDesk.transform, "Card Deck System")?.gameObject;
            if (deckObject == null)
            {
                throw new InvalidOperationException("The cloned second-room desk has no Card Deck System.");
            }

            CardSequenceRunner clonedRunner = deckObject.GetComponent<CardSequenceRunner>();
            StoryBlackboard clonedBlackboard = deckObject.GetComponent<StoryBlackboard>();
            if (clonedRunner != null)
            {
                UnityEngine.Object.DestroyImmediate(clonedRunner);
            }
            if (clonedBlackboard != null)
            {
                UnityEngine.Object.DestroyImmediate(clonedBlackboard);
            }
            refs.SecondPresenter = deckObject.GetComponent<CardDeckPresenter>();
            refs.SecondInteraction = deckObject.GetComponent<CardDeckInteraction>();
            refs.SecondLamp = BuildFluorescentCeilingRig(
                "Second Room Fluorescent Ceiling Rig",
                refs.SecondRoomSet.transform,
                SecondRoomCenterZ,
                22f);

            refs.FirstClockHand = BuildClock(
                "First Grandfather Clock",
                refs.FirstRoomSet.transform,
                new Vector3(RoomWidth * 0.5f - 0.11f, 1.75f, 0.7f),
                wood,
                brass);
            refs.SecondClockHand = BuildClock(
                "Second Grandfather Clock - Reverse",
                refs.SecondRoomSet.transform,
                new Vector3(RoomWidth * 0.5f - 0.11f, 1.75f, SecondRoomCenterZ + 0.7f),
                wood,
                brass);

            BuildNorthWallFeatures(
                refs.FirstRoomSet.transform,
                NorthWallZ,
                wall,
                doorMaterial,
                brass,
                nickel,
                windowMaterial,
                curtainMaterial,
                wallTrim,
                doorCreak,
                doorSlam,
                false,
                out HorrorDoorInteractable firstSecondDoor,
                out _,
                out GameObject cover,
                out Transform firstWindowTarget);
            refs.SecondDoor = firstSecondDoor;
            refs.SecondDoorCover = cover;
            refs.FirstRearRimAnchor = CreateMarker(
                refs.FirstRoomSet.transform,
                "First Room Rear Rim Anchor",
                new Vector3(FirstDoorX, 1.25f, NorthWallZ - 0.22f),
                Quaternion.identity);

            BuildNorthWallFeatures(
                refs.SecondRoomSet.transform,
                SecondNorthWallZ,
                wall,
                doorMaterial,
                brass,
                nickel,
                windowMaterial,
                curtainMaterial,
                wallTrim,
                doorCreak,
                doorSlam,
                true,
                out _,
                out HorrorDoorInteractable storyDoor,
                out _,
                out Transform secondWindowTarget);
            refs.StoryDoor = storyDoor;
            refs.WindowGazeTarget = secondWindowTarget;
            refs.SecondRearRimAnchor = CreateMarker(
                refs.SecondRoomSet.transform,
                "Second Room Rear Rim Anchor",
                new Vector3(FirstDoorX, 1.25f, SecondNorthWallZ - 0.22f),
                Quaternion.identity);

            refs.WindowVision = BuildWindowVision(
                refs.SecondRoomSet.transform,
                new Vector3(WindowX, 1.53f, SecondNorthWallZ - 0.14f),
                visionMaterial);
            refs.WindowVision.SetActive(false);
            BuildPreplacedCard(refs.SecondRoomSet.transform, new Vector3(-0.58f, 0.86f, SecondRoomCenterZ + 0.1f), doorMaterial);

            GameObject threat = BuildThreatEntity(
                "Approaching Silhouette",
                refs.SecondRoomSet.transform,
                new Vector3(0f, 0f, SecondNorthWallZ - 0.46f),
                silhouetteMaterial);
            refs.Threat = threat.transform;
            refs.ThreatStart = CreateMarker(
                refs.SecondRoomSet.transform,
                "Threat Start",
                new Vector3(0f, 0f, SecondNorthWallZ - 0.46f),
                Quaternion.identity);
            refs.ThreatEnd = CreateMarker(
                refs.SecondRoomSet.transform,
                "Threat End",
                new Vector3(0f, 0f, SecondRoomCenterZ - 0.86f),
                Quaternion.identity);

            refs.SilhouetteBacklight = BuildPointLight(
                "Silhouette Backlight",
                refs.SecondRoomSet.transform,
                new Vector3(0f, 1.55f, SecondNorthWallZ - 0.06f),
                new Color(0.56f, 0.64f, 0.75f),
                2.2f,
                2.6f,
                false);
            refs.MoonLight = BuildPointLight(
                "Curtain Moonlight",
                refs.FirstRoomSet.transform,
                new Vector3(WindowX, 1.55f, NorthWallZ - 0.42f),
                new Color(0.32f, 0.42f, 0.62f),
                0.32f,
                2.9f,
                false);
            refs.RearRimLight = BuildPointLight(
                "Rear Door Rim Light",
                refs.FirstRoomSet.transform,
                refs.FirstRearRimAnchor.position,
                new Color(0.48f, 0.6f, 0.78f),
                0.42f,
                1.8f,
                false);
            GameObject rearLightSlit = CreateCube(
                "Rear Door Cold Light Slit",
                refs.RearRimLight.transform,
                new Vector3(DoorWidth * 0.5f, 0f, 0.1f),
                new Vector3(0.018f, 1.9f, 0.025f),
                whiteLightMaterial,
                false);
            Renderer rearSlitRenderer = rearLightSlit.GetComponent<Renderer>();
            if (rearSlitRenderer != null)
            {
                rearSlitRenderer.enabled = false;
            }
            refs.ExitLight = BuildPointLight(
                "Exit White Light",
                refs.SecondRoomSet.transform,
                new Vector3(FirstDoorX, 1.35f, SecondNorthWallZ + 0.46f),
                Color.white,
                48f,
                5f,
                false);
            GameObject glowPanel = CreateCube(
                "Exit White Glow Panel",
                refs.ExitLight.transform,
                Vector3.zero,
                new Vector3(1.05f, 2.4f, 0.04f),
                whiteLightMaterial,
                false);
            glowPanel.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            Renderer glowRenderer = glowPanel.GetComponent<Renderer>();
            if (glowRenderer != null)
            {
                glowRenderer.enabled = false;
            }

            refs.PlayerStartMarker = CreateMarker(
                refs.FirstRoomSet.transform,
                "Exact Opening Camera Start",
                new Vector3(0f, 0.08f, 1.62f),
                Quaternion.Euler(0f, 180f, 0f));
            Vector3 secondSpawn = new Vector3(SecondDoorX, 0.08f, SecondRoomCenterZ - 1.74f);
            Vector3 towardTable = new Vector3(-SecondDoorX, 0f, 1.74f).normalized;
            refs.SecondRoomPlayerMarker = CreateMarker(
                refs.SecondRoomSet.transform,
                "Second Room Walk-In Camera",
                secondSpawn,
                Quaternion.LookRotation(towardTable, Vector3.up));
            player.SetPositionAndRotation(refs.PlayerStartMarker.position, refs.PlayerStartMarker.rotation);

            refs.SecondRoomZone = BuildZone(
                "Second Room Entry Zone",
                refs.SecondRoomSet.transform,
                new Vector3(SecondDoorX, 1f, NorthWallZ + 0.58f),
                new Vector3(DoorWidth, 2f, 0.72f),
                NarrativeZoneId.SecondRoom,
                player,
                true);
            refs.ReturnZone = BuildZone(
                "Return To First Room Zone",
                refs.FirstRoomSet.transform,
                new Vector3(SecondDoorX, 1f, NorthWallZ - 0.58f),
                new Vector3(DoorWidth, 2f, 0.72f),
                NarrativeZoneId.ReturnedToFirstRoom,
                player,
                false);
            refs.EndingZone = BuildZone(
                "Bright Exit Zone",
                refs.SecondRoomSet.transform,
                new Vector3(FirstDoorX, 1f, SecondNorthWallZ + 0.54f),
                new Vector3(DoorWidth, 2f, 0.86f),
                NarrativeZoneId.EndingCorridor,
                player,
                false);

            refs.EndingPortraitSilhouette = BuildPortrait(
                refs.FirstRoomSet.transform,
                new Vector3(-RoomWidth * 0.5f + 0.12f, 1.72f, 0.54f),
                wood,
                silhouetteMaterial);
            refs.EndingPortraitSilhouette.SetActive(false);
            return refs;
        }

        private static Renderer BuildFirstRoomShell(
            Transform parent,
            Material wall,
            Material floor,
            Material ceiling,
            Material ceilingGrid,
            Material wallTrim)
        {
            Renderer surfaceRenderer = BuildSharedShell(
                parent,
                0f,
                wall,
                floor,
                ceiling,
                ceilingGrid,
                wallTrim);
            CreateCube("First South Wall", parent, new Vector3(0f, 1.5f, -RoomDepth * 0.5f), new Vector3(RoomWidth, 3.2f, 0.18f), wall);
            CreateHorizontalBaseboard(
                parent,
                "First South Baseboard",
                -RoomWidth * 0.5f,
                RoomWidth * 0.5f,
                -RoomDepth * 0.5f + 0.115f,
                wallTrim);
            return surfaceRenderer;
        }

        private static Renderer BuildSecondRoomShell(
            Transform parent,
            Material wall,
            Material floor,
            Material ceiling,
            Material ceilingGrid,
            Material wallTrim)
        {
            Renderer surfaceRenderer = BuildSharedShell(
                parent,
                SecondRoomCenterZ,
                wall,
                floor,
                ceiling,
                ceilingGrid,
                wallTrim);
            float openingLeft = SecondDoorX - DoorWidth * 0.5f;
            float openingRight = SecondDoorX + DoorWidth * 0.5f;
            float leftWidth = openingLeft + RoomWidth * 0.5f;
            float rightWidth = RoomWidth * 0.5f - openingRight;
            CreateCube(
                "Second South Wall Left",
                parent,
                new Vector3(-RoomWidth * 0.5f + leftWidth * 0.5f, DoorHeight * 0.5f, NorthWallZ + RoomSeparation),
                new Vector3(leftWidth, DoorHeight, 0.18f),
                wall);
            CreateCube(
                "Second South Wall Right",
                parent,
                new Vector3(openingRight + rightWidth * 0.5f, DoorHeight * 0.5f, NorthWallZ + RoomSeparation),
                new Vector3(rightWidth, DoorHeight, 0.18f),
                wall);
            CreateCube(
                "Second South Lintel",
                parent,
                new Vector3(0f, DoorHeight + (RoomHeight - DoorHeight) * 0.5f, NorthWallZ + RoomSeparation),
                new Vector3(RoomWidth, RoomHeight - DoorHeight, 0.18f),
                wall);
            float interiorBaseboardZ = NorthWallZ + RoomSeparation + 0.115f;
            CreateHorizontalBaseboard(
                parent,
                "Second South Baseboard Left",
                -RoomWidth * 0.5f,
                openingLeft,
                interiorBaseboardZ,
                wallTrim);
            CreateHorizontalBaseboard(
                parent,
                "Second South Baseboard Right",
                openingRight,
                RoomWidth * 0.5f,
                interiorBaseboardZ,
                wallTrim);
            return surfaceRenderer;
        }

        private static Renderer BuildSharedShell(
            Transform parent,
            float centerZ,
            Material wall,
            Material floor,
            Material ceiling,
            Material ceilingGrid,
            Material wallTrim)
        {
            GameObject floorCollider = CreateCube(
                "Backrooms Carpet Collider",
                parent,
                new Vector3(0f, -0.1f, centerZ),
                new Vector3(RoomWidth, 0.2f, RoomDepth),
                floor);
            GameObject ceilingCollider = CreateCube(
                "Drop Ceiling Collider",
                parent,
                new Vector3(0f, RoomHeight + 0.1f, centerZ),
                new Vector3(RoomWidth, 0.2f, RoomDepth),
                ceiling);
            SetRendererEnabled(floorCollider, false);
            SetRendererEnabled(ceilingCollider, false);
            Renderer surfaceRenderer = BuildBackroomsAssetSurfaceShell(parent, centerZ, floor);
            BuildCeilingPerimeterFrame(parent, centerZ, ceilingGrid);
            CreateCube("West Wallpaper Wall", parent, new Vector3(-RoomWidth * 0.5f, 1.5f, centerZ), new Vector3(0.18f, 3.2f, RoomDepth), wall);
            CreateCube("East Wallpaper Wall", parent, new Vector3(RoomWidth * 0.5f, 1.5f, centerZ), new Vector3(0.18f, 3.2f, RoomDepth), wall);
            CreateCube(
                "West Wall Baseboard",
                parent,
                new Vector3(-RoomWidth * 0.5f + 0.115f, 0.08f, centerZ),
                new Vector3(0.07f, 0.16f, RoomDepth - 0.2f),
                wallTrim,
                false);
            CreateCube(
                "East Wall Baseboard",
                parent,
                new Vector3(RoomWidth * 0.5f - 0.115f, 0.08f, centerZ),
                new Vector3(0.07f, 0.16f, RoomDepth - 0.2f),
                wallTrim,
                false);
            return surfaceRenderer;
        }

        private static Renderer BuildBackroomsAssetSurfaceShell(
            Transform parent,
            float centerZ,
            Material floorMaterial)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackroomsSurfacePrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"The backrooms floor and ceiling prefab is missing at '{BackroomsSurfacePrefabPath}'.");
            }

            GameObject surface = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (surface == null)
            {
                surface = UnityEngine.Object.Instantiate(prefab, parent, false);
            }

            surface.name = "Backrooms Asset Pack Floor + Ceiling";
            surface.transform.localPosition = new Vector3(0f, 0f, centerZ);
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = new Vector3(
                RoomWidth / BackroomsSurfaceWidth,
                RoomHeight / BackroomsSurfaceHeight,
                RoomDepth / BackroomsSurfaceDepth);

            foreach (Collider collider in surface.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer surfaceRenderer = surface.GetComponentInChildren<Renderer>(true);
            if (surfaceRenderer == null)
            {
                throw new InvalidOperationException(
                    $"The backrooms surface prefab at '{BackroomsSurfacePrefabPath}' has no renderer.");
            }

            Material[] surfaceMaterials = surfaceRenderer.sharedMaterials;
            if (surfaceMaterials.Length < 2)
            {
                throw new InvalidOperationException(
                    $"The backrooms surface prefab at '{BackroomsSurfacePrefabPath}' must expose separate floor and ceiling material slots.");
            }

            surfaceMaterials[0] = floorMaterial;
            surfaceRenderer.sharedMaterials = surfaceMaterials;
            return surfaceRenderer;
        }

        private static void BuildCeilingPerimeterFrame(Transform parent, float centerZ, Material material)
        {
            const float railThickness = 0.035f;
            float undersideY = RoomHeight - railThickness * 0.5f;
            CreateCube(
                "Ceiling Perimeter West",
                parent,
                new Vector3(-RoomWidth * 0.5f + railThickness * 0.5f, undersideY, centerZ),
                new Vector3(railThickness, railThickness, RoomDepth),
                material,
                false);
            CreateCube(
                "Ceiling Perimeter East",
                parent,
                new Vector3(RoomWidth * 0.5f - railThickness * 0.5f, undersideY, centerZ),
                new Vector3(railThickness, railThickness, RoomDepth),
                material,
                false);
            CreateCube(
                "Ceiling Perimeter South",
                parent,
                new Vector3(0f, undersideY, centerZ - RoomDepth * 0.5f + railThickness * 0.5f),
                new Vector3(RoomWidth, railThickness, railThickness),
                material,
                false);
            CreateCube(
                "Ceiling Perimeter North",
                parent,
                new Vector3(0f, undersideY, centerZ + RoomDepth * 0.5f - railThickness * 0.5f),
                new Vector3(RoomWidth, railThickness, railThickness),
                material,
                false);
        }

        private static void BuildNorthWallFeatures(
            Transform parent,
            float wallZ,
            Material wall,
            Material doorMaterial,
            Material brass,
            Material nickel,
            Material windowMaterial,
            Material curtainMaterial,
            Material wallTrim,
            AudioClip doorCreak,
            AudioClip doorSlam,
            bool secondRoom,
            out HorrorDoorInteractable secondDoor,
            out HorrorDoorInteractable storyDoor,
            out GameObject cover,
            out Transform windowTarget)
        {
            secondDoor = null;
            storyDoor = null;
            cover = null;
            float leftEdge = -RoomWidth * 0.5f;
            float rightEdge = RoomWidth * 0.5f;
            float firstLeft = FirstDoorX - DoorWidth * 0.5f;
            float firstRight = FirstDoorX + DoorWidth * 0.5f;
            float windowLeft = WindowX - DoorWidth * 0.5f;
            float windowRight = WindowX + DoorWidth * 0.5f;
            float secondLeft = SecondDoorX - DoorWidth * 0.5f;
            float secondRight = SecondDoorX + DoorWidth * 0.5f;

            CreateWallSegment(parent, "North Far Left", leftEdge, firstLeft, wallZ, wall);
            CreateWallSegment(parent, "North Between First And Window", firstRight, windowLeft, wallZ, wall);
            CreateWallSegment(parent, "North Between Window And Second", windowRight, secondLeft, wallZ, wall);
            CreateWallSegment(parent, "North Far Right", secondRight, rightEdge, wallZ, wall);
            CreateCube(
                "North Door Lintel",
                parent,
                new Vector3(0f, DoorHeight + (RoomHeight - DoorHeight) * 0.5f, wallZ),
                new Vector3(RoomWidth, RoomHeight - DoorHeight, 0.18f),
                wall);
            CreateCube(
                "Window Sill Wall",
                parent,
                new Vector3(WindowX, 0.4f, wallZ),
                new Vector3(DoorWidth, 0.8f, 0.18f),
                wall);
            float interiorBaseboardZ = wallZ - 0.115f;
            CreateHorizontalBaseboard(parent, "North Baseboard Far Left", leftEdge, firstLeft, interiorBaseboardZ, wallTrim);
            CreateHorizontalBaseboard(parent, "North Baseboard Between First And Window", firstRight, windowLeft, interiorBaseboardZ, wallTrim);
            CreateHorizontalBaseboard(parent, "North Baseboard Below Window", windowLeft, windowRight, interiorBaseboardZ, wallTrim);
            CreateHorizontalBaseboard(parent, "North Baseboard Between Window And Second", windowRight, secondLeft, interiorBaseboardZ, wallTrim);
            CreateHorizontalBaseboard(parent, "North Baseboard Far Right", secondRight, rightEdge, interiorBaseboardZ, wallTrim);

            if (secondRoom)
            {
                storyDoor = BuildDoor(
                    "First Door - Story Exit",
                    parent,
                    new Vector3(FirstDoorX, 0f, wallZ - 0.08f),
                    DoorWidth,
                    doorMaterial,
                    brass,
                    doorCreak,
                    doorSlam,
                    false);
                BuildFixedDoor(
                    "Second Door Copy - Closed",
                    parent,
                    new Vector3(SecondDoorX, DoorHeight * 0.5f, wallZ - 0.08f),
                    doorMaterial,
                    nickel);
            }
            else
            {
                BuildFixedDoor(
                    "First Door - Locked Brass",
                    parent,
                    new Vector3(FirstDoorX, DoorHeight * 0.5f, wallZ - 0.08f),
                    doorMaterial,
                    brass);
                secondDoor = BuildDoor(
                    "Second Door Pivot",
                    parent,
                    new Vector3(SecondDoorX, 0f, wallZ - 0.08f),
                    DoorWidth,
                    doorMaterial,
                    nickel,
                    doorCreak,
                    doorSlam,
                    true);
                cover = CreateCube(
                    "Second Door Concealing Wallpaper Wall",
                    parent,
                    new Vector3(SecondDoorX, DoorHeight * 0.5f, wallZ - 0.05f),
                    new Vector3(DoorWidth + 0.04f, DoorHeight, 0.08f),
                    wall,
                    false);
            }

            BuildWindow(
                secondRoom ? "Second Room Window" : "First Room Window",
                parent,
                new Vector3(WindowX, 1.52f, wallZ - 0.1f),
                windowMaterial,
                curtainMaterial,
                out windowTarget);
        }

        private static void CreateWallSegment(Transform parent, string name, float start, float end, float z, Material wall)
        {
            float width = Mathf.Max(0.02f, end - start);
            CreateCube(name, parent, new Vector3((start + end) * 0.5f, DoorHeight * 0.5f, z), new Vector3(width, DoorHeight, 0.18f), wall);
        }

        private static void CreateHorizontalBaseboard(
            Transform parent,
            string name,
            float start,
            float end,
            float z,
            Material material)
        {
            float width = Mathf.Max(0.02f, end - start);
            CreateCube(
                name,
                parent,
                new Vector3((start + end) * 0.5f, 0.08f, z),
                new Vector3(width, 0.16f, 0.07f),
                material,
                false);
        }

        private static HorrorDoorInteractable BuildDoor(
            string name,
            Transform parent,
            Vector3 centerBottom,
            float width,
            Material doorMaterial,
            Material handleMaterial,
            AudioClip openClip,
            AudioClip slamClip,
            bool openTowardFirstRoom)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = centerBottom + Vector3.left * (width * 0.5f);
            GameObject panel = CreateCube(
                "Door Panel",
                pivot.transform,
                Vector3.zero,
                new Vector3(width, DoorHeight, 0.1f),
                doorMaterial);
            panel.transform.localPosition = new Vector3(width * 0.5f, DoorHeight * 0.5f, 0f);
            GameObject handle = CreatePrimitive(PrimitiveType.Sphere, "Door Handle", panel.transform, false, handleMaterial);
            handle.transform.localPosition = new Vector3(width * 0.35f, 0f, -0.09f);
            handle.transform.localScale = Vector3.one * 0.075f;
            Transform interactionPoint = CreateMarker(
                panel.transform,
                "Door Interaction Point",
                new Vector3(width * 0.35f, 0f, -0.16f),
                Quaternion.identity,
                true);
            if (!openTowardFirstRoom)
            {
                Vector3 escapeGuardPosition = pivot.transform.localPosition
                    + new Vector3(width * 0.455f, 1.433f, 0.407f);
                GameObject escapeGuard = CreateCube(
                    "Story Exit Escape Guard",
                    parent,
                    escapeGuardPosition,
                    new Vector3(width + 0.2f, 0.1f, 0.1f),
                    null);
                Renderer escapeGuardRenderer = escapeGuard.GetComponent<Renderer>();
                if (escapeGuardRenderer != null)
                {
                    UnityEngine.Object.DestroyImmediate(escapeGuardRenderer);
                }
            }
            AudioSource source = pivot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 0.6f;
            source.maxDistance = 9f;
            HorrorDoorInteractable door = pivot.AddComponent<HorrorDoorInteractable>();
            SerializedObject serialized = new SerializedObject(door);
            Set(serialized, "pivot", pivot.transform);
            Set(serialized, "handle", handle.transform);
            Set(serialized, "interactionPoint", interactionPoint);
            Set(serialized, "interactionEnabled", false);
            Set(serialized, "openAngle", openTowardFirstRoom ? -102f : 102f);
            Set(serialized, "partialOpenAngle", openTowardFirstRoom ? -14f : 14f);
            Set(serialized, "openDuration", 0.72f);
            Set(serialized, "openSound", openClip);
            Set(serialized, "slamSound", slamClip);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        private static void BuildFixedDoor(string name, Transform parent, Vector3 position, Material door, Material handleMaterial)
        {
            GameObject panel = CreateCube(name, parent, position, new Vector3(DoorWidth, DoorHeight, 0.1f), door);
            GameObject handle = CreatePrimitive(PrimitiveType.Sphere, "Handle", panel.transform, false, handleMaterial);
            handle.transform.localPosition = new Vector3(0.34f, 0f, -0.09f);
            handle.transform.localScale = Vector3.one * 0.075f;
        }

        private static void BuildWindow(
            string name,
            Transform parent,
            Vector3 position,
            Material windowMaterial,
            Material curtainMaterial,
            out Transform target)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            CreateCube("Black Glass", root.transform, Vector3.zero, new Vector3(DoorWidth, 1.42f, 0.035f), windowMaterial, false);
            CreateCube("Frame Left", root.transform, new Vector3(-DoorWidth * 0.53f, 0f, -0.02f), new Vector3(0.075f, 1.55f, 0.08f), curtainMaterial, false);
            CreateCube("Frame Right", root.transform, new Vector3(DoorWidth * 0.53f, 0f, -0.02f), new Vector3(0.075f, 1.55f, 0.08f), curtainMaterial, false);
            CreateCube("Frame Top", root.transform, new Vector3(0f, 0.74f, -0.02f), new Vector3(0.98f, 0.075f, 0.08f), curtainMaterial, false);
            CreateCube("Frame Bottom", root.transform, new Vector3(0f, -0.74f, -0.02f), new Vector3(0.98f, 0.075f, 0.08f), curtainMaterial, false);
            CreateCube("Curtain Left", root.transform, new Vector3(-0.33f, 0.08f, -0.08f), new Vector3(0.18f, 1.42f, 0.05f), curtainMaterial, false);
            CreateCube("Curtain Right Half", root.transform, new Vector3(0.36f, 0.3f, -0.08f), new Vector3(0.13f, 0.96f, 0.05f), curtainMaterial, false);
            target = CreateMarker(root.transform, "Window Gaze Target", Vector3.zero, Quaternion.identity, true);
        }

        private static Light BuildFluorescentCeilingRig(
            string name,
            Transform parent,
            float centerZ,
            float intensity)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, 2.62f, centerZ);

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.98f, 0.9f);
            light.useColorTemperature = true;
            light.colorTemperature = 3600f;
            light.intensity = intensity;
            light.range = 8.2f;
            light.bounceIntensity = 0.8f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            return light;
        }

        private static Transform BuildClock(string name, Transform parent, Vector3 position, Material wood, Material metal)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, -90f, 0f));
            CreateCube("Clock Case", root.transform, Vector3.zero, new Vector3(0.72f, 1.72f, 0.12f), wood, false);
            GameObject face = CreatePrimitive(PrimitiveType.Cylinder, "Clock Face", root.transform, false, metal);
            face.transform.localPosition = new Vector3(0f, 0.43f, -0.09f);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            face.transform.localScale = new Vector3(0.28f, 0.025f, 0.28f);
            Transform hand = CreateMarker(root.transform, "Clock Hand Pivot", new Vector3(0f, 0.43f, -0.13f), Quaternion.identity, true);
            CreateCube("Clock Hand", hand, new Vector3(0f, 0.11f, 0f), new Vector3(0.025f, 0.23f, 0.02f), wood, false);
            return hand;
        }

        private static GameObject BuildWindowVision(Transform parent, Vector3 position, Material material)
        {
            GameObject root = new GameObject("Window Room Echo - 25 Percent");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            CreateCube("Echo Table", root.transform, new Vector3(0f, -0.12f, 0f), new Vector3(0.54f, 0.08f, 0.015f), material, false);
            CreateCube("Echo Lamp", root.transform, new Vector3(0.18f, 0.12f, 0f), new Vector3(0.04f, 0.42f, 0.015f), material, false);
            CreateCube("Echo Door", root.transform, new Vector3(0.29f, 0.3f, 0f), new Vector3(0.22f, 0.66f, 0.015f), material, false);
            return root;
        }

        private static void BuildPreplacedCard(Transform parent, Vector3 position, Material material)
        {
            GameObject root = new GameObject("Already Drawn Card Four");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
            Material fallbackFace = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/DoNotDraw/Materials/Cards/CardFront.mat") ?? material;
            Texture2D faceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PreplacedCardTexturePath);
            Material cardFace = GetOrCreatePreplacedCardMaterial(fallbackFace, faceTexture);
            CreateCube(
                "Card Four Surface",
                root.transform,
                Vector3.zero,
                new Vector3(0.42f, 0.015f, 0.62f),
                cardFace,
                false);
            if (faceTexture != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Card Four Text");
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0.011f, 0f);
            canvasObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.006f;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(70f, 90f);
            Text text = new GameObject("Label").AddComponent<Text>();
            text.transform.SetParent(canvasObject.transform, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "DO NOT OPEN\nTHE SECOND DOOR.";
            text.fontSize = 10;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.09f, 0.035f, 0.025f, 1f);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private static Material GetOrCreatePreplacedCardMaterial(
            Material fallback,
            Texture2D faceTexture)
        {
            if (faceTexture == null)
            {
                return fallback;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(PreplacedCardMaterialPath);
            Shader shader = fallback != null
                ? fallback.shader
                : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, PreplacedCardMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", faceTexture);
                material.SetTextureScale("_BaseMap", Vector2.one);
            }
            material.mainTexture = faceTexture;
            material.mainTextureScale = Vector2.one;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildThreatEntity(
            string name,
            Transform parent,
            Vector3 position,
            Material material)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            GameObject entityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThreatEntityPrefabPath);
            if (entityPrefab == null)
            {
                throw new InvalidOperationException(
                    $"The approaching threat prefab is missing at '{ThreatEntityPrefabPath}'.");
            }

            GameObject visual = PrefabUtility.InstantiatePrefab(entityPrefab, root.transform) as GameObject;
            if (visual == null)
            {
                visual = UnityEngine.Object.Instantiate(entityPrefab, root.transform, false);
            }
            visual.name = "Backrooms Entity Visual";
            visual.transform.localPosition = entityPrefab.transform.localPosition;
            visual.transform.localRotation = entityPrefab.transform.localRotation;
            visual.transform.localScale = entityPrefab.transform.localScale;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"The approaching threat prefab at '{ThreatEntityPrefabPath}' contains no renderers.");
            }
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                }
                else
                {
                    for (int index = 0; index < materials.Length; index++)
                    {
                        materials[index] = material;
                    }
                    renderer.sharedMaterials = materials;
                }
                renderer.receiveShadows = false;
            }

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            return root;
        }

        private static Light BuildPointLight(
            string name,
            Transform parent,
            Vector3 position,
            Color color,
            float intensity,
            float range,
            bool enabled)
        {
            GameObject owner = new GameObject(name);
            owner.transform.SetParent(parent, false);
            owner.transform.position = position;
            Light light = owner.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            light.enabled = enabled;
            return light;
        }

        private static NarrativeZoneTrigger BuildZone(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            NarrativeZoneId id,
            Transform player,
            bool enabled)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = position;
            BoxCollider collider = zone.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
            Rigidbody body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            NarrativeZoneTrigger trigger = zone.AddComponent<NarrativeZoneTrigger>();
            SerializedObject serialized = new SerializedObject(trigger);
            serialized.FindProperty("zoneId").enumValueIndex = (int)id;
            Set(serialized, "playerRoot", player);
            Set(serialized, "triggerEnabled", enabled);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        private static GameObject BuildPortrait(Transform parent, Vector3 position, Material frame, Material silhouette)
        {
            GameObject root = new GameObject("Ending Portrait Silhouette");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 90f, 0f));
            CreateCube("Portrait Back", root.transform, Vector3.zero, new Vector3(0.58f, 0.88f, 0.04f), frame, false);
            CreateCube("Portrait Body", root.transform, new Vector3(0f, -0.12f, -0.035f), new Vector3(0.24f, 0.48f, 0.03f), silhouette, false);
            GameObject head = CreatePrimitive(PrimitiveType.Sphere, "Portrait Head", root.transform, false, silhouette);
            head.transform.localPosition = new Vector3(0f, 0.22f, -0.04f);
            head.transform.localScale = new Vector3(0.2f, 0.24f, 0.04f);
            return root;
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool keepCollider = true)
        {
            GameObject cube = CreatePrimitive(PrimitiveType.Cube, name, parent, keepCollider, material);
            cube.transform.localPosition = position;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = scale;
            return cube;
        }

        private static void SetRendererEnabled(GameObject owner, bool enabled)
        {
            Renderer renderer = owner != null ? owner.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            bool keepCollider,
            Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            if (!keepCollider)
            {
                Collider collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }
            return gameObject;
        }

        private static Transform CreateMarker(
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            bool local = false)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            if (local)
            {
                marker.transform.localPosition = position;
                marker.transform.localRotation = rotation;
            }
            else
            {
                marker.transform.SetPositionAndRotation(position, rotation);
            }
            return marker.transform;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform found = FindChildRecursive(parent.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void RemoveChildrenNamed(Transform parent, string name)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                RemoveChildrenNamed(child, name);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }
    }
}
