using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Protocol
{
    public sealed class SelectionPortraits : MonoBehaviour
    {
        private readonly Dictionary<Transform, RenderTexture> portraits = new Dictionary<Transform, RenderTexture>();
        private Texture2D harvesterIcon;

        public Texture Get(Transform source)
        {
            if (source == null)
                return null;
            var robot = source.GetComponent<RobotController>();
            if (robot != null && robot.UnitType == "Harvester")
            {
                if (harvesterIcon == null)
                    harvesterIcon = Resources.Load<Texture2D>("UI/PortraitHarvester");
                if (harvesterIcon != null)
                    return harvesterIcon;
            }

            return portraits.TryGetValue(source, out var texture) ? texture : null;
        }

        private void Start()
        {
            var world = transform.Find("World");
            var cameraObject = new GameObject("Portrait camera");
            cameraObject.transform.position = new Vector3(0, -1000, -10);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.09f, .14f, .18f);
            camera.farClipPlane = 100;
            var sunObject = new GameObject("Portrait light");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.cullingMask = 1 << 31;
            sun.transform.rotation = Quaternion.Euler(40, -30, 0);
            foreach (var light in world.GetComponentsInChildren<Light>())
                light.cullingMask &= ~(1 << 31);
            world.GetComponentInChildren<Camera>().cullingMask &= ~(1 << 31);
            foreach (var unit in world.GetComponentsInChildren<RobotController>())
                Render(unit.transform, camera);
            foreach (var building in world.GetComponentsInChildren<SelectableStructure>())
                Render(building.transform, camera);
            cameraObject.SetActive(false);
            sunObject.SetActive(false);
            Destroy(cameraObject);
            Destroy(sunObject);
        }

        private void Render(Transform source, Camera camera)
        {
            var preview = new GameObject("Portrait model");
            preview.transform.position = new Vector3(0, -1000, 0);
            var materials = new List<Material>();
            Bounds bounds = new Bounds(preview.transform.position, Vector3.zero);
            bool first = true;
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var original = filter.GetComponent<MeshRenderer>();
                if (original == null || !original.enabled)
                    continue;
                var part = new GameObject(filter.name);
                part.layer = 31;
                part.transform.SetParent(preview.transform, false);
                part.transform.localPosition = source.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation = Quaternion.Inverse(source.rotation) * filter.transform.rotation;
                part.transform.localScale = filter.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = part.AddComponent<MeshRenderer>();
                var list = original.sharedMaterials;
                for (int i = 0; i < list.Length; i++)
                {
                    list[i] = new Material(list[i])
                    {
                        shader = Resources.Load<Shader>("FogSurface")
                    };
                    list[i].SetFloat("_IgnoreWarFog", 1);
                    materials.Add(list[i]);
                }

                renderer.sharedMaterials = list;
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            camera.orthographicSize = Mathf.Max(.5f, bounds.extents.magnitude * .85f);
            camera.transform.position = bounds.center + new Vector3(1, .7f, -1.4f).normalized * (bounds.extents.magnitude * 3 + 3);
            camera.transform.LookAt(bounds.center);
            var texture = new RenderTexture(160, 160, 24)
            {
                name = "Portrait " + source.name,
                antiAliasing = 2
            };
            camera.targetTexture = texture;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            camera.targetTexture = null;
            portraits.Add(source, texture);
            preview.SetActive(false);
            Destroy(preview);
            foreach (var material in materials)
                Destroy(material);
        }

        private void OnDestroy()
        {
            foreach (var texture in portraits.Values)
            {
                texture.Release();
                Destroy(texture);
            }

            portraits.Clear();
        }
    }
}
