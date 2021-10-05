using UnityEngine;
using UnityEditor;
using Unity.Collections;
//using Unity.Entities;
//using Unity.Transforms;
//using Unity.Rendering;
//using Unity.Mathematics;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Burst;
using System.Text;
using Unity.Mathematics;
using System.Globalization;

namespace doot
{
    public static class Utilities
    {

        public static void Shuffle<T>(this IList<T> list)
        {
            System.Random rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static void ChangeGameobjectLayers(GameObject parentGO, int layerInt, bool changeChildren = true)
        {
            parentGO.layer = layerInt;
            if (changeChildren)
            {
                foreach (Transform child in parentGO.transform)
                    child.gameObject.layer = layerInt;
            }
        }

        public static float Remap(float from, float fromMin, float fromMax, float toMin, float toMax)
        {
            float normal = Mathf.InverseLerp(fromMin, fromMax, from);
            return Mathf.Lerp(toMin, toMax, normal);
        }

        #region Raycast batch job
        [BurstCompile]
        struct PrepareRaycastCommandsJob : IJobParallelFor
        {
            [DeallocateOnJobCompletion] public NativeArray<RaycastCommand> raycastCommmands;
            [DeallocateOnJobCompletion] [Unity.Collections.ReadOnly] public NativeArray<Vector3> originPoints;
            [DeallocateOnJobCompletion] [Unity.Collections.ReadOnly] public NativeArray<Vector3> directions;
            [Unity.Collections.ReadOnly] public float length;
            [Unity.Collections.ReadOnly] public LayerMask layerMask;

            public void Execute(int i)
            {
                raycastCommmands[i] = new RaycastCommand(originPoints[i], directions[i], length, layerMask);
            }
        }
        #endregion

        #region Raycast Batch
        public static RaycastHit[] GetBatchedRaycasts(NativeArray<Vector3> origins, NativeArray<Vector3> directions, float length, LayerMask layerMask)
        {
            var raycastCommands = new NativeArray<RaycastCommand>(origins.Length, Allocator.TempJob);
            var raycastHits = new NativeArray<RaycastHit>(origins.Length, Allocator.TempJob);

            PrepareRaycastCommandsJob prepareRaycastCommandsJob = new PrepareRaycastCommandsJob()
            {
                raycastCommmands = raycastCommands,
                originPoints = origins,
                directions = directions,
                length = length,
                layerMask = layerMask
            };

            for (int i = 0; i < origins.Length; i++)
            {
                raycastCommands[i] = new RaycastCommand(origins[i], directions[i], length, layerMask);
            }

            JobHandle jobHandle = RaycastCommand.ScheduleBatch(raycastCommands, raycastHits, 1);

            jobHandle.Complete();

            RaycastHit[] raycastHitsResults = raycastHits.ToArray();

            //  cleanup
            raycastCommands.Dispose();
            raycastHits.Dispose();

            return raycastHitsResults;
        }
        #endregion

        public static void SetGlobalScale(this Transform transform, Vector3 globalScale)
        {
            transform.localScale = Vector3.one;
            transform.localScale = new Vector3(globalScale.x / transform.lossyScale.x, globalScale.y / transform.lossyScale.y, globalScale.z / transform.lossyScale.z);
        }

        //  CALL IN EDITOR ONLY! This finds all scriptable objects of specific in project and returns a array of them. 
        //  Ex.) SomeScriptableObject[] items = GetAllScriptableObjects<SomeScriptableObject>();
        public static T[] GetScriptableObjectsInProject<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);  //FindAssets uses tags check documentation for more info
            T[] a = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)         //probably could get optimized 
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                a[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return a;
        }

        #region Convert number into big number notation string
        public static string BigNumberNotation(float value, string lowDecimalFormat = "N0")
        {
            string[] notations = new string[12] { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
            int baseValue = 0;
            string notationValue = "";
            string toStringValue;

            if (value >= 10000) // I start using the first notation at 10k
            {
                value /= 1000;
                baseValue++;
                while (Mathf.Round((float)value) >= 1000)
                {
                    value /= 1000;
                    baseValue++;
                }

                if (baseValue < 2)
                    toStringValue = "N1"; // display 1 decimal while under 1 million
                else
                    toStringValue = "N2"; // display 2 decimals for 1 million and higher

                if (baseValue > notations.Length) return null;
                else notationValue = notations[baseValue];
            }
            else toStringValue = lowDecimalFormat; // string formatting at low numbers

            StringBuilder builder = new StringBuilder();
            builder.Append(value.ToString(toStringValue));
            builder.Append(notationValue);
            return builder.ToString();
        }
        #endregion

        #region Return round vector3 (float precision correction)
        public static Vector3 GetRoundedVector3(Vector3 v, int decimalPlaces = 1)
        {
            return new Vector3(
                    (float)Math.Round(v.x, decimalPlaces),
                    (float)Math.Round(v.y, decimalPlaces),
                    (float)Math.Round(v.z, decimalPlaces)
                    );
        }
        #endregion

        #region String manipulations
        public static string GetTitleCase(string title)
        {
            return new CultureInfo("en").TextInfo.ToTitleCase(title.ToLower().Replace("_", " "));
        }

        public static string GetSubstringBeforeCharacter(this string text, string delimiter = "_")
        {
            if (!String.IsNullOrWhiteSpace(text))
            {
                int charLocation = text.IndexOf(delimiter, StringComparison.Ordinal);

                if (charLocation > 0)
                {
                    return text.Substring(0, charLocation);
                }
            }

            return String.Empty;
        }
        #endregion

        #region FunctionRateLimiter(): Return true if the frameToWait is completed. Used for running functions in Update every X frames. 
        /* Ex) return true if 30 frames have past:
         * --------------------------------------------------
         * int cycleFrequency = 30;
         * 
         * void Update()
         * {
         *      if (Utilities.RateLimiter(cycleFrequency))
         *      {
         *          //this will happen once per 30 frames
         *      }
         * }
         * --------------------------------------------------
         */
        public static bool FunctionRateLimiter(uint framesToWait)
        {
            if (Time.frameCount % framesToWait == 0)
                return true;
            else
                return false;
        }
        #endregion

        #region Get uniform points of a circle
        public static Vector3[] GetUniformCirclePoints(int amount, float radius, Vector3 centerPos)
        {
            Vector3[] points = new Vector3[amount];
            for (int i = 0; i < amount; i++)
            {
                /* Distance around the circle */
                float radians = 2 * math.PI / amount * i;

                /* Get the vector direction */
                float vertical = math.sin(radians);
                float horizontal = math.cos(radians);

                var spawnDir = new Vector3(horizontal, vertical, 0);

                /* Get the circle position */
                Vector3 pos = centerPos + spawnDir * radius; // Radius is just the distance away from the point

                points[i] = pos;
            }

            return points;
        }
        #endregion

        #region Get uniform points of a grid
        public static Vector3[] GetUniformGridPoints(int amount, float spacing, Vector3 centerPos)
        {
            //  variables
            Vector3[] points = new Vector3[amount];
            int columns = Mathf.RoundToInt(Mathf.Sqrt((float)amount));
            int rows = 0;
            float posX = 0;
            float posY = 0;

            //  spawn pos
            for (int i = 0; i < amount; i++)
            {
                Vector3 pos = new Vector3(posX, posY, 0) + centerPos;

                posX += spacing;

                //  reach max columns? start new row
                if ((i+1) % columns == 0)
                {
                    posX = 0;
                    posY -= spacing;
                    rows++;
                }

                points[i] = pos;
            }

            //  center pos
            for (int i = 0; i < points.Length; i++)
            {
                float xOffset = 0.5f + (columns / -2.0f);
                float yOffset = -0.5f + (rows / 2.0f);
                xOffset *= spacing;
                yOffset *= spacing;
                points[i] += new Vector3(xOffset, yOffset, 0);
            }

            return points;
        }
        #endregion

        #region Get grid points between a start position and end position
        public static List<Vector3> GetGridPointsBetweenTwoPoints(Vector3 startPos, Vector3 endPos)
        {
            List<Vector3> positions = new List<Vector3>();
            int lengthX = Mathf.Abs(Mathf.RoundToInt(startPos.x - endPos.x)) + 1;
            int lengthZ = Mathf.Abs(Mathf.RoundToInt(startPos.z - endPos.z)) + 1;
            for (int x = 0; x < lengthX; x++)
            {
                for (int z = 0; z < lengthZ; z++)
                {
                    //  determine direction
                    Vector3 _pos = new Vector3(startPos.x, 0, endPos.z);
                    _pos.x += startPos.x - endPos.x < 0 ? x : -x;
                    _pos.z += startPos.z - endPos.z < 0 ? -z : z;

                    positions.Add(_pos);
                }
            }

            return positions;
        }
        #endregion

        #region Physics
        //  [BUG] [TODO] when timeStep is higher then 1, the trajectory tends to be inaccurate the higher it is from the pointAmount.
        public static Vector3[] GetVelocityTrajectoryPoints(Vector3 startPos, Vector3 velocity, int pointAmount = 20, float timeStep = 1)
        {
            Vector3[] points = new Vector3[pointAmount];

            Vector3 _currentPos = startPos;
            Vector3 _currentVelocity = velocity;

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = _currentPos;
                _currentVelocity += Physics.gravity * Time.fixedDeltaTime * timeStep;
                _currentPos += _currentVelocity * Time.fixedDeltaTime * timeStep;
                //_currentVelocity += Physics.gravity * Time.fixedDeltaTime;
                //_currentPos += _currentVelocity * Time.fixedDeltaTime;
            }

            return points;
        }
        #endregion

        #region Animation
        //public static void AddAnimationEvent(Animator animator, int Clip, float time, string functionName, float floatParameter)
        //{
        //    AnimationEvent animationEvent = new AnimationEvent();
        //    animationEvent.functionName = functionName;
        //    animationEvent.floatParameter = floatParameter;
        //    animationEvent.time = time;
        //    animator.runtimeAnimatorController.animationClips[Clip].AddEvent(animationEvent);
        //}

        public static bool AddAnimationEvent(Animator animator, string clipName, float time, string functionName)
        {
            return AddAnimationEvent<UnityEngine.Object>(animator, clipName, time, functionName, null);
        }

        public static bool AddAnimationEvent<T>(Animator animator, string clipName, float time, string functionName, T parameter)
        {
            bool found = false;
            AnimationClip animationClip = null;
            foreach (AnimationClip aniclip in animator.runtimeAnimatorController.animationClips)
                if (aniclip.name.Equals(clipName))
                {
                    animationClip = aniclip;
                    found = true;
                    break;
                }
            if (found == false) return false;

            AnimationEvent animationEvent = new AnimationEvent();
            animationEvent.functionName = functionName;
            animationEvent.time = time;
            if (!(parameter is UnityEngine.Object && parameter == null))
                switch (parameter)
                {
                    case int p: animationEvent.intParameter = p; break;
                    case float p: animationEvent.floatParameter = p; break;
                    case string p: animationEvent.stringParameter = p; break;
                    case UnityEngine.Object p: animationEvent.objectReferenceParameter = p; break;
                    default:
                        break;
                }
            animationClip.AddEvent(animationEvent);

            return true;
        }
        #endregion
    }

    public static class ShapeUtil
    {
        //  OLD VERSION
        //public static Vector3[] GetPointsInRadius(Vector3 centerPoint, int radius, bool offsetCenter = true)
        //{
        //    HashSet<Vector3> newPoints = new HashSet<Vector3>();

        //    //assuming that the center of each grid is actually offset by gridsize/2 (meaning, (0,0) is on a grid corner, not a grid center)
        //    //this can be optimized by predeterming a narrow range of values, (i.e. a min for each)
        //    if (offsetCenter)
        //    {
        //        centerPoint += new Vector3(0.5f, 0, 0.5f);
        //    }

        //    for (int x = radius / 2 * -1; x <= radius / 2; x++)
        //    {
        //        for (int y = radius / 2 * -1; y <= radius / 2; y++)
        //        {
        //            Vector3 point = new Vector3(centerPoint.x + x, centerPoint.y, centerPoint.z + y);
        //            Vector3 heading = point - centerPoint;

        //            //  proximity check. if target is within range...
        //            if (heading.sqrMagnitude <= radius && !newPoints.Contains(point))
        //            {
        //                newPoints.Add(point);
        //            }
        //        }
        //    }

        //    //return count;
        //    return newPoints.ToArray();
        //}

        public static Vector3[] GetPointsInRadius(Vector3 center, int radius, bool offsetCenter = true, bool getEdgesOnly = false)
        {
            HashSet<Vector3> points = new HashSet<Vector3>();

            for (int j = Mathf.RoundToInt(center.x) - radius; j <= center.x + radius; j++)
            {
                for (int k = Mathf.RoundToInt(center.z) - radius; k <= center.z + radius; k++)
                {
                    float distance = Vector3.Distance(new Vector3(j, 0, k), new Vector3(center.x, 0, center.z));
                    if (distance <= radius/* + 0.5f*/)
                    {
                        if (getEdgesOnly)
                        {
                            if (distance >= radius - 1f)
                            {
                                Vector3 pos = offsetCenter ? new Vector3(j + 0.5f, 0, k + 0.5f) : new Vector3(j, 0, k);
                                points.Add(pos);
                            }
                        }
                        else
                        {
                            Vector3 pos =  offsetCenter? new Vector3(j + 0.5f, 0, k + 0.5f) : new Vector3(j, 0, k);
                            points.Add(pos);
                        }
                    }
                }
            }

            return points.ToArray();
        }

        public static Vector3[] GetGridPointsInSqaure(Vector3 centerPoint, int length)
        {
            List<Vector3> newPoints = new List<Vector3>();

            //assuming that the center of each grid is actually offset by gridsize/2 (meaning, (0,0) is on a grid corner, not a grid center)
            //this can be optimized by predeterming a narrow range of values, (i.e. a min for each)

            for (int x = length / 2 * -1; x <= length / 2; x++)
            {
                for (int y = length / 2 * -1; y <= length / 2; y++)
                {
                    Vector3 point = new Vector3(centerPoint.x + x, centerPoint.y, centerPoint.z + y);
                    if (!newPoints.Contains(point))
                    {
                        newPoints.Add(point);
                    }
                }
            }

            //return count;
            return newPoints.ToArray();
        }
    }

    public static class Scenes
    {
        public const string main_menu = "main_menu";
        public const string dev_room = "dev_room";
        public const string dungeon_level = "dungeon_level";
    }

    public static class Tags
    {
        public const string Player = "Player";
        public const string Trees_Parent = "Trees_Parent";
        public const string Rocks_Parent = "Rocks_Parent";
        public const string Plot = "Plot";
        public const string Interactable = "Interactable";
        public const string Socket = "Socket";
        public const string Arrow = "Arrow";
        public const string OtherTriggerIgnoreThisTrigger = "OtherTriggerIgnoreThisTrigger";
        public const string UIStackText = "UIStackText";
        public const string spawnPoint = "SpawnPoint";

        //  A* Pathfinding specific tags
        public const int AStar_Ground = 0;
        public const int AStar_Road = 1;
        public const int AStar_Building = 2;
        public const int AStar_Resource = 3;

        //  A* Pathfinding specific tag path masks
        public const int AStar_Ground_Mask = 1 << AStar_Ground;
        public const int AStar_Road_Mask = 1 << AStar_Road;
        public const int AStar_Building_Mask = 1 << AStar_Building;
        public const int AStar_Resource_Mask = 1 << AStar_Resource;
    }

    public static class Layers
    {
        // A list of tag strings. 
        public const int IIgnorePlayer = 27;
        public const int IIgnoreAllExceptPlayer = 28;
        public const int Itest = 29;
        public const int IDefault = 0;
        public const int IGround = 9;
        public const int IWater = 4;
        public const int IBuilding = 9;
        public const int IUnit = 10;

        //  Used for raycasting layermasking
        public static readonly LayerMask Water = 1 << 4;
        public static readonly LayerMask PostProcessing = 1 << 8;
        public static readonly LayerMask Ground = 1 << 9;
        public static readonly LayerMask Unit = 1 << 10;
        public static readonly LayerMask Building = 1 << 11;
    }
}
