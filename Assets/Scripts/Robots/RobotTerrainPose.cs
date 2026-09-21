using System.Collections.Generic;
using UnityEngine;

namespace Protocol
{
    [DisallowMultipleComponent]
    public sealed class RobotTerrainPose : MonoBehaviour
    {
        private struct Part
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly List<Part> parts = new List<Part>();
        private Collider terrain;
        private Quaternion tilt = Quaternion.identity;
        private Quaternion targetTilt = Quaternion.identity;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private bool needsPose = true;
        private bool initialized;

        private void Start()
        {
            CaptureParts();
            UpdatePose(true);
        }

        private void OnTransformChildrenChanged()
        {
            if (initialized)
                CaptureParts();
        }

        private void CaptureParts()
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<MeshRenderer>() == null && child.GetComponent<Light>() == null)
                    continue;
                bool known = false;
                foreach (var part in parts)
                    if (part.Transform == child)
                    {
                        known = true;
                        break;
                    }
                if (!known)
                    parts.Add(new Part
                    {
                        Transform = child,
                        Position = child.localPosition,
                        Rotation = child.localRotation
                    });
            }
            initialized = true;
            needsPose = true;
        }

        public bool TryGetSurfacePoint(Vector3 position, out Vector3 point)
        {
            if (terrain == null)
            {
                var world = transform.parent != null ? transform.parent.parent : null;
                var ground = world != null ? world.Find("Terrain") : null;
                if (ground != null)
                    terrain = ground.GetComponent<Collider>();
            }

            if (terrain != null && terrain.enabled)
            {
                Bounds bounds = terrain.bounds;
                var ray = new Ray(new Vector3(position.x, bounds.max.y + 1, position.z), Vector3.down);
                if (terrain.Raycast(ray, out RaycastHit hit, bounds.size.y + 2))
                {
                    point = hit.point;
                    return true;
                }
            }

            point = position;
            return false;
        }

        private void LateUpdate()
        {
            if (initialized)
                UpdatePose(false);
        }

        private void UpdatePose(bool snap)
        {
            Vector3 position = transform.position;
            if (!snap && !needsPose && position == lastPosition && transform.rotation == lastRotation &&
                Quaternion.Angle(tilt, targetTilt) < .01f)
                return;
            if (!TryGetSurfacePoint(position, out Vector3 center) ||
                !TryGetSurfacePoint(position + transform.right * .8f, out Vector3 right) ||
                !TryGetSurfacePoint(position - transform.right * .8f, out Vector3 left) ||
                !TryGetSurfacePoint(position + transform.forward * .9f, out Vector3 front) ||
                !TryGetSurfacePoint(position - transform.forward * .9f, out Vector3 back))
                return;

            Vector3 normal = Vector3.Cross(front - back, right - left).normalized;
            if (normal.sqrMagnitude < .5f || normal.y <= 0)
                return;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            targetTilt = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(forward, normal);
            tilt = snap ? targetTilt : Quaternion.Slerp(tilt, targetTilt, 1 - Mathf.Exp(-12 * Time.deltaTime));

            Vector3 offset = transform.InverseTransformVector(center - position);
            float lift = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 contact = transform.TransformPoint(offset + tilt * new Vector3(x * .99f, .05f, z * .95f));
                    if (TryGetSurfacePoint(contact, out Vector3 ground))
                        lift = Mathf.Max(lift, ground.y + .02f - contact.y);
                }
            offset += transform.InverseTransformVector(Vector3.up * lift);
            foreach (var part in parts)
            {
                if (part.Transform == null)
                    continue;
                part.Transform.localPosition = offset + tilt * part.Position;
                part.Transform.localRotation = tilt * part.Rotation;
            }
            lastPosition = position;
            lastRotation = transform.rotation;
            needsPose = false;
        }
    }
}
