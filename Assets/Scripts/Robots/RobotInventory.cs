using UnityEngine;

namespace Protocol
{
    public sealed class RobotInventory : MonoBehaviour
    {
        public int Capacity { get; private set; } = 10;

        public int Cargo { get; private set; }

        public ResourceKind Kind { get; private set; }

        public bool IsFull => Cargo >= Capacity;

        public void Initialize(int capacity) => Capacity = Mathf.Max(1, capacity);

        public bool CanAccept(ResourceKind kind) => !IsFull && (Cargo == 0 || Kind == kind);

        public bool TryAddUnit() => TryAddUnit(ResourceKind.Iron);

        public bool TryAddUnit(ResourceKind kind)
        {
            if (!CanAccept(kind))
                return false;
            Kind = kind;
            Cargo++;
            return true;
        }

        public int DepositInto(ResourceManager resources)
        {
            if (resources == null || !resources.isActiveAndEnabled)
                return 0;
            int amount = Cargo;
            resources.Add(Kind, amount);
            Cargo = 0;
            return amount;
        }
    }
}
