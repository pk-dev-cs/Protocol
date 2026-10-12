using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Protocol.Editor
{
    internal sealed class RobotModelGeometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Face(Vector3[] points, Vector3 outward, Vector3 center, Quaternion rotation)
        {
            int start = vertices.Count;
            bool reverse = Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), outward) < 0;
            for (int i = 0; i < points.Length; i++)
            {
                vertices.Add(center + rotation * points[i]);
                normals.Add(rotation * outward.normalized);
                uv.Add(new Vector2(i == 1 || i == 2 ? 1 : 0, i >= 2 ? 1 : 0));
            }
            for (int i = 1; i < points.Length - 1; i++)
            {
                triangles.Add(start);
                triangles.Add(start + (reverse ? i + 1 : i));
                triangles.Add(start + (reverse ? i : i + 1));
            }
        }

        public void Box(Vector3 center, Vector3 size, float bevel, Quaternion rotation)
        {
            Vector3 h = size * .5f;
            float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .8f);
            Vector3 inner = h - Vector3.one * b;
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var points = new Vector3[4];
                    for (int i = 0; i < 4; i++)
                    {
                        points[i][axis] = h[axis] * sign;
                        points[i][u] = inner[u] * (i == 0 || i == 3 ? -1 : 1);
                        points[i][v] = inner[v] * (i < 2 ? -1 : 1);
                    }
                    Vector3 normal = Vector3.zero;
                    normal[axis] = sign;
                    Face(points, normal, center, rotation);
                }
                for (int su = -1; su <= 1; su += 2)
                    for (int sv = -1; sv <= 1; sv += 2)
                    {
                        var points = new Vector3[4];
                        for (int i = 0; i < 4; i++)
                        {
                            points[i][axis] = inner[axis] * (i < 2 ? -1 : 1);
                            points[i][u] = (i == 0 || i == 3 ? h[u] : inner[u]) * su;
                            points[i][v] = (i == 0 || i == 3 ? inner[v] : h[v]) * sv;
                        }
                        Vector3 normal = Vector3.zero;
                        normal[u] = su;
                        normal[v] = sv;
                        Face(points, normal, center, rotation);
                    }
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var corner = Vector3.Scale(inner, new Vector3(sx, sy, sz));
                        Face(new[] { corner + Vector3.right * b * sx, corner + Vector3.up * b * sy,
                            corner + Vector3.forward * b * sz }, new Vector3(sx, sy, sz), center, rotation);
                    }
        }

        public void Cylinder(Vector3 center, float radius, float length, Vector3 axis, int segments = 20)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                var na = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var nb = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                int start = vertices.Count;
                Vector3[] points = { na * radius - Vector3.up * length / 2,
                    na * radius + Vector3.up * length / 2, nb * radius + Vector3.up * length / 2,
                    nb * radius - Vector3.up * length / 2 };
                for (int j = 0; j < 4; j++)
                {
                    vertices.Add(center + rotation * points[j]);
                    normals.Add(rotation * (j < 2 ? na : nb));
                    uv.Add(new Vector2((i + (j < 2 ? 0 : 1)) / (float)segments, j == 1 || j == 2 ? 1 : 0));
                }
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                Face(new[] { na * radius + Vector3.up * length / 2, Vector3.up * length / 2,
                    nb * radius + Vector3.up * length / 2 }, Vector3.up, center, rotation);
                Face(new[] { na * radius - Vector3.up * length / 2, Vector3.down * length / 2,
                    nb * radius - Vector3.up * length / 2 }, Vector3.down, center, rotation);
            }
        }

        public void Bar(Vector3 start, Vector3 end, float width, float height, float bevel)
        {
            Box((start + end) * .5f, new Vector3(width, height, Vector3.Distance(start, end)), bevel,
                Quaternion.LookRotation(end - start));
        }

        public void Rod(Vector3 start, Vector3 end, float radius, int segments = 12) =>
            Cylinder((start + end) * .5f, radius, Vector3.Distance(start, end), end - start, segments);
    }
}