using Unity.Entities;

namespace GAS.Runtime
{
    public readonly struct ASCReadModel
    {
        private readonly ASCHandle _handle;
        private readonly bool _isReadable;
        private readonly bool _hasLevel;
        private readonly int _level;
        private readonly bool _hasTagMask;
        private readonly TagMaskComponent _tagMask;
        private readonly AttributeValueBuffer[] _attributes;
        private readonly GasPresentationEventView[] _presentationEvents;

        internal ASCReadModel(EntityManager entityManager, Entity entity)
        {
            _handle = new ASCHandle(entity);
            _isReadable = CanCapture(entityManager, entity);
            _hasLevel = false;
            _level = 0;
            _hasTagMask = false;
            _tagMask = default;
            _attributes = null;
            _presentationEvents = null;

            if (!_isReadable)
                return;

            if (entityManager.HasComponent<ASCIdentityComponent>(entity))
            {
                _hasLevel = true;
                _level = entityManager.GetComponentData<ASCIdentityComponent>(entity).Level;
            }

            if (entityManager.HasComponent<TagMaskComponent>(entity))
            {
                _hasTagMask = true;
                _tagMask = entityManager.GetComponentData<TagMaskComponent>(entity);
            }

            if (entityManager.HasBuffer<AttributeValueBuffer>(entity))
            {
                var attributes = entityManager.GetBuffer<AttributeValueBuffer>(entity);
                _attributes = new AttributeValueBuffer[attributes.Length];
                for (var i = 0; i < attributes.Length; i++)
                    _attributes[i] = attributes[i];
            }

            if (entityManager.HasBuffer<PresentationEventBuffer>(entity))
            {
                var events = entityManager.GetBuffer<PresentationEventBuffer>(entity);
                _presentationEvents = new GasPresentationEventView[events.Length];
                for (var i = 0; i < events.Length; i++)
                {
                    _presentationEvents[i] = new GasPresentationEventView
                    {
                        Event = events[i],
                    };
                }
            }
        }

        internal ASCReadModel(EntityManager entityManager, ASCHandle handle)
            : this(entityManager, handle.RuntimeEntity)
        {
        }

        public ASCHandle Handle => _handle;

        public bool IsReadable => _isReadable;

        public int GetLevel()
        {
            return _hasLevel ? _level : 0;
        }

        public bool TryGetLevel(out int level)
        {
            if (!_hasLevel)
            {
                level = 0;
                return false;
            }

            level = _level;
            return true;
        }

        public bool HasTag(int tag)
        {
            if (TryGetTagMask(out var mask))
                return mask.HasTag(tag);
            return false;
        }

        public bool TryGetTagMask(out TagMaskComponent mask)
        {
            if (!_hasTagMask)
            {
                mask = default;
                return false;
            }

            mask = _tagMask;
            return true;
        }

        public float GetAttributeValue(int attrSetCode, int attrCode)
        {
            TryGetAttributeValue(attrSetCode, attrCode, out var value);
            return value;
        }

        public bool TryGetAttributeValue(int attrSetCode, int attrCode, out float value)
        {
            return TryGetAttributeCurrentValue(attrSetCode, attrCode, out value);
        }

        public bool TryGetAttributeCurrentValue(int attrSetCode, int attrCode, out float value)
        {
            if (_attributes == null)
            {
                value = 0f;
                return false;
            }

            for (var i = 0; i < _attributes.Length; i++)
            {
                if (_attributes[i].AttrSetCode == attrSetCode && _attributes[i].Code == attrCode)
                {
                    value = _attributes[i].CurrentValue;
                    return true;
                }
            }

            value = 0f;
            return false;
        }

        public int PresentationEventCount
        {
            get
            {
                return _presentationEvents?.Length ?? 0;
            }
        }

        public int CopyPresentationEvents(GasPresentationEventView[] output)
        {
            if (output == null
                || output.Length == 0
                || _presentationEvents == null)
            {
                return 0;
            }

            var count = _presentationEvents.Length < output.Length ? _presentationEvents.Length : output.Length;
            for (var i = 0; i < count; i++)
                output[i] = _presentationEvents[i];

            return count;
        }

        private static bool CanCapture(EntityManager entityManager, Entity entity)
        {
            return entityManager.World != null
                   && entityManager.World.IsCreated
                   && entity != Entity.Null
                   && entityManager.Exists(entity);
        }
    }
}
