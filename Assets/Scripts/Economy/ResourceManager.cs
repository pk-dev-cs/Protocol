using UnityEngine;

namespace Protocol
{
    public enum ResourceKind
    {
        Iron,
        Wood
    }

    public sealed class ResourceManager : MonoBehaviour
    {
        public int Iron { get; private set; }

        public int Wood { get; private set; }

        public void AddIron(int amount) => Add(ResourceKind.Iron, amount);

        public void Add(ResourceKind kind, int amount)
        {
            if (amount < 0)
                throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (kind == ResourceKind.Iron)
                Iron = checked(Iron + amount);
            else
                Wood = checked(Wood + amount);
        }

        public bool TrySpendWood(int amount)
        {
            if (amount < 0)
                throw new System.ArgumentOutOfRangeException(nameof(amount));
            if (Wood < amount)
                return false;
            Wood -= amount;
            return true;
        }
    }
}
