using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Protocol
{
    /// <summary>One runtime bake for this small, static scenario.</summary>
    public sealed class ScenarioNavigation : MonoBehaviour
    {
        private NavMeshData data;
        private NavMeshDataInstance instance;
        public void Initialize(Transform world)

        {
            if (instance.valid)
                return;
            var barrier = world.Find("Navigation barrier");
            if (barrier == null)
            {
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Navigation barrier";
                obstacle.transform.SetParent(world, false);
                obstacle.transform.localPosition = new Vector3(3, 1.2f, -2.5f);
                obstacle.transform.localScale = new Vector3(1.4f, 2.4f, 6);
                obstacle.GetComponent<Renderer>().sharedMaterial = world.Find("Base/Foundation").GetComponent<Renderer>().sharedMaterial;
            }

            var robotRoot = world.Find("Robots");
            var sources = new List<NavMeshBuildSource>();
            var exclusions = new List<NavMeshBuildMarkup>
            {
                new NavMeshBuildMarkup
                {
                    root = robotRoot,
                    ignoreFromBuild = true
                },
                new NavMeshBuildMarkup
                {
                    root = world.Find("Trees"),
                    ignoreFromBuild = true
                },
                new NavMeshBuildMarkup
                {
                    root = world.Find("Terrain markings"),
                    ignoreFromBuild = true
                }
            };
            var details = world.Find("Planet detail study");
            if (details != null)
                foreach (Transform detail in details)
                {
                    if (detail.name != "Basalt outcrop")
                        continue;
                    EnsureBoulderCollider(detail.gameObject);
                    exclusions.Add(new NavMeshBuildMarkup
                    {
                        root = detail,
                        overrideArea = true,
                        area = 1 // Unity's built-in Not Walkable area.
                    });
                }
            Physics.SyncTransforms();
            NavMeshBuilder.CollectSources(world, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, exclusions, sources);
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 1.1f;
            settings.agentHeight = 2;
            settings.agentClimb = 0.2f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.2f;
            data = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                new Bounds(new Vector3(0, 16, 0), new Vector3(ScenarioLandscape.Size + 4, 64, ScenarioLandscape.Size + 4)),
                world.position,
                world.rotation);
            if (data == null)
                throw new System.InvalidOperationException("Nie udało się utworzyć NavMesh sceny.");
            instance = NavMesh.AddNavMeshData(data);
            int index = 0;
            foreach (Transform robot in robotRoot)
            {
                var target = new GameObject(robot.name + " - mine destination").transform;
                target.SetParent(world.Find("Mine"), false);
                target.localPosition = new Vector3(index == 0 ? -1.3f : 1.3f, 0, -4.5f);
                var agent = robot.gameObject.AddComponent<NavMeshAgent>();
                agent.agentTypeID = settings.agentTypeID;
                agent.radius = 1f;
                agent.height = 2;
                agent.baseOffset = 0;
                agent.speed = 4;
                agent.acceleration = 12;
                agent.stoppingDistance = 0.15f;
                agent.autoBraking = true;
                agent.updateRotation = false; // Primitive robot's front points along local -Z.
                agent.avoidancePriority = 40 + index * 10;
                var movement = robot.gameObject.AddComponent<RobotMovement>();
                movement.Initialize(agent);
                robot.GetComponent<RobotController>().InitializeMovement(movement, target);
                index++;
            }
        }

        public static void EnsureBoulderCollider(GameObject boulder)
        {
            var filter = boulder.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;
            var collider = boulder.GetComponent<MeshCollider>();
            if (collider == null)
                collider = boulder.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            collider.enabled = true;
        }

        private void OnDestroy()

        {
            if (instance.valid)
                instance.Remove();
            if (data != null)
                Destroy(data);
        }
    }
}
