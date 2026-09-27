using UnityEngine;
using UnityEngine.UI;

namespace Protocol
{
    public sealed class LoadingTopography : MaskableGraphic
    {
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            for (int island = 0; island < 2; island++)
            {
                var center = new Vector2(bounds.xMin + bounds.width * (island == 0 ? .13f : .87f),
                    bounds.yMin + bounds.height * (island == 0 ? .3f : .75f));
                for (int ring = 1; ring <= 16; ring++)
                {
                    Vector2 previous = Point(center, 0, ring, island, bounds.height);
                    for (int step = 1; step <= 100; step++)
                    {
                        var next = Point(center, step * Mathf.PI * 2 / 100, ring, island, bounds.height);
                        Line(mesh, previous, next, .65f);
                        previous = next;
                    }
                }
            }
        }

        private static Vector2 Point(Vector2 center, float angle, int ring, int island, float height)
        {
            float radius = height * (.055f + ring * .029f);
            float relief = 1 + .12f * Mathf.Sin(angle * 3 + island) + .07f * Mathf.Cos(angle * 5 + ring * .12f);
            return center + new Vector2(Mathf.Cos(angle) * 1.35f, Mathf.Sin(angle)) * radius * relief;
        }

        private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width)
        {
            var direction = (to - from).normalized;
            var normal = new Vector2(-direction.y, direction.x) * width * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(from - normal, color, Vector2.zero);
            mesh.AddVert(from + normal, color, Vector2.zero);
            mesh.AddVert(to + normal, color, Vector2.zero);
            mesh.AddVert(to - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
