using System;
using Unity.Entities;

namespace GAS.Runtime
{
    public readonly struct ASCHandle : IEquatable<ASCHandle>
    {
        private readonly Entity _entity;

        internal ASCHandle(Entity entity)
        {
            _entity = entity;
        }

        public bool IsValid => _entity != Entity.Null;

        internal Entity RuntimeEntity => _entity;

        internal bool TryResolveRuntimeEntity(out Entity entity)
        {
            entity = _entity;
            return IsValid;
        }

        internal bool MatchesRuntimeEntity(Entity entity)
        {
            return IsValid && _entity == entity;
        }

        public bool Equals(ASCHandle other)
        {
            return _entity.Equals(other._entity);
        }

        public override bool Equals(object obj)
        {
            return obj is ASCHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _entity.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? _entity.ToString() : "ASCHandle.Null";
        }
    }
}
