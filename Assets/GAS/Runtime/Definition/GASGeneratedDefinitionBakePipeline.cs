using System;
using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public readonly struct GASGeneratedDefinitionCarrierArtifact
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionBakeWritePhase SourcePhase;
        public readonly GASGeneratedDefinitionBakeWriteTarget SourceTarget;

        public GASGeneratedDefinitionCarrierArtifact(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakeWritePhase sourcePhase,
            GASGeneratedDefinitionBakeWriteTarget sourceTarget)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            SourcePhase = sourcePhase;
            SourceTarget = sourceTarget;
        }
    }

    public readonly struct GASGeneratedDefinitionBakerInputArtifact
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionBakeWritePhase SourcePhase;
        public readonly GASGeneratedDefinitionBakeWriteTarget SourceTarget;

        public GASGeneratedDefinitionBakerInputArtifact(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakeWritePhase sourcePhase,
            GASGeneratedDefinitionBakeWriteTarget sourceTarget)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            SourcePhase = sourcePhase;
            SourceTarget = sourceTarget;
        }
    }

    public readonly struct GASGeneratedDefinitionStaticBlobCacheRequest
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionBakeWritePhase SourcePhase;
        public readonly GASGeneratedDefinitionBakeWriteTarget SourceTarget;

        public GASGeneratedDefinitionStaticBlobCacheRequest(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakeWritePhase sourcePhase,
            GASGeneratedDefinitionBakeWriteTarget sourceTarget)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            SourcePhase = sourcePhase;
            SourceTarget = sourceTarget;
        }
    }

    public readonly struct GASGeneratedDefinitionRuntimeArchetypeArtifact
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionArchetypeTemplateKind TemplateKind;
        public readonly GASGeneratedDefinitionArchetypeSlot Slots;
        public readonly GASGeneratedDefinitionBakingBoundary DeferredBoundaries;

        public GASGeneratedDefinitionRuntimeArchetypeArtifact(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionArchetypeTemplateKind templateKind,
            GASGeneratedDefinitionArchetypeSlot slots,
            GASGeneratedDefinitionBakingBoundary deferredBoundaries)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            TemplateKind = templateKind;
            Slots = slots;
            DeferredBoundaries = deferredBoundaries;
        }

        public bool HasSlot(GASGeneratedDefinitionArchetypeSlot slot)
        {
            return slot != GASGeneratedDefinitionArchetypeSlot.None
                   && (Slots & slot) == slot;
        }

        public bool HasDeferredBoundary(GASGeneratedDefinitionBakingBoundary boundary)
        {
            return boundary != GASGeneratedDefinitionBakingBoundary.None
                   && (DeferredBoundaries & boundary) == boundary;
        }
    }

    public readonly struct GASGeneratedDefinitionDeferredBoundaryArtifact
    {
        public readonly GASDefinitionKind DefinitionKind;
        public readonly int DefinitionCode;
        public readonly GASGeneratedDefinitionBakingBoundary Boundary;
        public readonly GASGeneratedDefinitionBakeWritePhase SourcePhase;
        public readonly GASGeneratedDefinitionBakeWriteTarget SourceTarget;

        public GASGeneratedDefinitionDeferredBoundaryArtifact(
            GASDefinitionKind definitionKind,
            int definitionCode,
            GASGeneratedDefinitionBakingBoundary boundary,
            GASGeneratedDefinitionBakeWritePhase sourcePhase,
            GASGeneratedDefinitionBakeWriteTarget sourceTarget)
        {
            DefinitionKind = definitionKind;
            DefinitionCode = definitionCode;
            Boundary = boundary;
            SourcePhase = sourcePhase;
            SourceTarget = sourceTarget;
        }
    }

    public readonly struct GASGeneratedDefinitionBakeResult
    {
        private readonly GASGeneratedDefinitionCarrierArtifact[] _carriers;
        private readonly GASGeneratedDefinitionBakerInputArtifact[] _bakerInputs;
        private readonly GASGeneratedDefinitionStaticBlobCacheRequest[] _staticBlobCacheRequests;
        private readonly GASGeneratedDefinitionRuntimeArchetypeArtifact[] _runtimeArchetypes;
        private readonly GASGeneratedDefinitionDeferredBoundaryArtifact[] _deferredBoundaries;

        public readonly GASGeneratedDefinitionBakeContract SourceContract;
        public readonly bool CanRunUnityBaker;
        public readonly int CandidateWriteCount;
        public readonly int SkippedIneligibleWriteCount;
        public readonly int SkippedIneligibleArchetypeTemplateCount;

        public GASGeneratedDefinitionBakeResult(
            GASGeneratedDefinitionBakeContract sourceContract,
            bool canRunUnityBaker,
            int candidateWriteCount,
            int skippedIneligibleWriteCount,
            int skippedIneligibleArchetypeTemplateCount,
            IEnumerable<GASGeneratedDefinitionCarrierArtifact> carriers,
            IEnumerable<GASGeneratedDefinitionBakerInputArtifact> bakerInputs,
            IEnumerable<GASGeneratedDefinitionStaticBlobCacheRequest> staticBlobCacheRequests,
            IEnumerable<GASGeneratedDefinitionRuntimeArchetypeArtifact> runtimeArchetypes,
            IEnumerable<GASGeneratedDefinitionDeferredBoundaryArtifact> deferredBoundaries)
        {
            SourceContract = sourceContract;
            CanRunUnityBaker = canRunUnityBaker;
            CandidateWriteCount = candidateWriteCount;
            SkippedIneligibleWriteCount = skippedIneligibleWriteCount;
            SkippedIneligibleArchetypeTemplateCount = skippedIneligibleArchetypeTemplateCount;
            _carriers = Materialize(carriers);
            _bakerInputs = Materialize(bakerInputs);
            _staticBlobCacheRequests = Materialize(staticBlobCacheRequests);
            _runtimeArchetypes = Materialize(runtimeArchetypes);
            _deferredBoundaries = Materialize(deferredBoundaries);
        }

        public static GASGeneratedDefinitionBakeResult Empty =>
            new(
                GASGeneratedDefinitionBakeContract.Empty,
                false,
                0,
                0,
                0,
                Array.Empty<GASGeneratedDefinitionCarrierArtifact>(),
                Array.Empty<GASGeneratedDefinitionBakerInputArtifact>(),
                Array.Empty<GASGeneratedDefinitionStaticBlobCacheRequest>(),
                Array.Empty<GASGeneratedDefinitionRuntimeArchetypeArtifact>(),
                Array.Empty<GASGeneratedDefinitionDeferredBoundaryArtifact>());

        public IReadOnlyList<GASGeneratedDefinitionCarrierArtifact> Carriers =>
            _carriers ?? Array.Empty<GASGeneratedDefinitionCarrierArtifact>();

        public IReadOnlyList<GASGeneratedDefinitionBakerInputArtifact> BakerInputs =>
            _bakerInputs ?? Array.Empty<GASGeneratedDefinitionBakerInputArtifact>();

        public IReadOnlyList<GASGeneratedDefinitionStaticBlobCacheRequest> StaticBlobCacheRequests =>
            _staticBlobCacheRequests ?? Array.Empty<GASGeneratedDefinitionStaticBlobCacheRequest>();

        public IReadOnlyList<GASGeneratedDefinitionRuntimeArchetypeArtifact> RuntimeArchetypes =>
            _runtimeArchetypes ?? Array.Empty<GASGeneratedDefinitionRuntimeArchetypeArtifact>();

        public IReadOnlyList<GASGeneratedDefinitionDeferredBoundaryArtifact> DeferredBoundaries =>
            _deferredBoundaries ?? Array.Empty<GASGeneratedDefinitionDeferredBoundaryArtifact>();

        public int CarrierCount => Carriers.Count;
        public int BakerInputCount => BakerInputs.Count;
        public int StaticBlobCacheRequestCount => StaticBlobCacheRequests.Count;
        public int RuntimeArchetypeCount => RuntimeArchetypes.Count;
        public int DeferredBoundaryCount => DeferredBoundaries.Count;
        public bool HasSkippedMaterialization =>
            SkippedIneligibleWriteCount > 0 || SkippedIneligibleArchetypeTemplateCount > 0;

        private static T[] Materialize<T>(IEnumerable<T> values)
        {
            if (values == null)
                return Array.Empty<T>();

            return values is T[] array ? (T[])array.Clone() : new List<T>(values).ToArray();
        }
    }

    public readonly struct GASGeneratedDefinitionStaticBlobCacheWarmupResult
    {
        public readonly int RequestCount;
        public readonly int MaterializedCount;
        public readonly int FailedCount;
        public readonly int SkippedCount;

        public GASGeneratedDefinitionStaticBlobCacheWarmupResult(
            int requestCount,
            int materializedCount,
            int failedCount,
            int skippedCount)
        {
            RequestCount = requestCount;
            MaterializedCount = materializedCount;
            FailedCount = failedCount;
            SkippedCount = skippedCount;
        }

        public bool AllRequestsMaterialized => FailedCount == 0 && SkippedCount == 0;
    }

    public static class GASGeneratedDefinitionBakePipeline
    {
        public static GASGeneratedDefinitionBakeResult Create(GASGeneratedDefinitionBakeContract contract)
        {
            var carriers = new List<GASGeneratedDefinitionCarrierArtifact>();
            var bakerInputs = new List<GASGeneratedDefinitionBakerInputArtifact>();
            var staticBlobCacheRequests = new List<GASGeneratedDefinitionStaticBlobCacheRequest>();
            var runtimeArchetypes = new List<GASGeneratedDefinitionRuntimeArchetypeArtifact>();
            var deferredBoundaries = new List<GASGeneratedDefinitionDeferredBoundaryArtifact>();
            var skippedIneligibleWrites = 0;

            var writes = contract.Writes;
            for (var i = 0; i < writes.Count; i++)
            {
                var write = writes[i];
                if (write.WriteKind == GASGeneratedDefinitionBakeWriteKind.DeferredBoundary)
                {
                    deferredBoundaries.Add(new GASGeneratedDefinitionDeferredBoundaryArtifact(
                        write.DefinitionKind,
                        write.DefinitionCode,
                        write.Boundary,
                        write.WritePhase,
                        write.WriteTarget));
                    continue;
                }

                if (!write.IsEligible)
                {
                    skippedIneligibleWrites++;
                    continue;
                }

                MaterializeWrite(
                    write,
                    carriers,
                    bakerInputs,
                    staticBlobCacheRequests,
                    ref skippedIneligibleWrites);
            }

            var skippedIneligibleTemplates = 0;
            var templates = contract.ArchetypeTemplates;
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (!template.IsEligible)
                {
                    skippedIneligibleTemplates++;
                    continue;
                }

                runtimeArchetypes.Add(new GASGeneratedDefinitionRuntimeArchetypeArtifact(
                    template.DefinitionKind,
                    template.DefinitionCode,
                    template.TemplateKind,
                    template.Slots,
                    template.DeferredBoundaries));
            }

            return new GASGeneratedDefinitionBakeResult(
                contract,
                contract.CanRunUnityBaker,
                contract.WriteCount,
                skippedIneligibleWrites,
                skippedIneligibleTemplates,
                carriers,
                bakerInputs,
                staticBlobCacheRequests,
                runtimeArchetypes,
                deferredBoundaries);
        }

        public static GASGeneratedDefinitionStaticBlobCacheWarmupResult WarmupStaticDefinitionBlobCache(
            EntityManager entityManager,
            GASGeneratedDefinitionBakeContract contract)
        {
            return WarmupStaticDefinitionBlobCache(entityManager, Create(contract));
        }

        public static GASGeneratedDefinitionStaticBlobCacheWarmupResult WarmupStaticDefinitionBlobCache(
            EntityManager entityManager,
            GASGeneratedDefinitionBakeResult result)
        {
            var requests = result.StaticBlobCacheRequests;
            var requestCount = 0;
            var materializedCount = 0;
            var failedCount = 0;
            var skippedCount = 0;

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.DefinitionKind != GASDefinitionKind.GameplayEffect || request.DefinitionCode <= 0)
                {
                    skippedCount++;
                    continue;
                }

                requestCount++;
                if (GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                        entityManager,
                        request.DefinitionCode,
                        out _,
                        new ConfigRegistryReferenceContext(
                            ConfigRegistryConfigKind.None,
                            0,
                            ConfigRegistryReferenceKind.Direct)))
                {
                    materializedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            return new GASGeneratedDefinitionStaticBlobCacheWarmupResult(
                requestCount,
                materializedCount,
                failedCount,
                skippedCount);
        }

        private static void MaterializeWrite(
            GASGeneratedDefinitionBakeWrite write,
            List<GASGeneratedDefinitionCarrierArtifact> carriers,
            List<GASGeneratedDefinitionBakerInputArtifact> bakerInputs,
            List<GASGeneratedDefinitionStaticBlobCacheRequest> staticBlobCacheRequests,
            ref int skippedIneligibleWrites)
        {
            switch (write.WriteKind)
            {
                case GASGeneratedDefinitionBakeWriteKind.GeneratedCarrier:
                    carriers.Add(new GASGeneratedDefinitionCarrierArtifact(
                        write.DefinitionKind,
                        write.DefinitionCode,
                        write.WritePhase,
                        write.WriteTarget));
                    break;
                case GASGeneratedDefinitionBakeWriteKind.UnityBakerInput:
                    bakerInputs.Add(new GASGeneratedDefinitionBakerInputArtifact(
                        write.DefinitionKind,
                        write.DefinitionCode,
                        write.WritePhase,
                        write.WriteTarget));
                    break;
                case GASGeneratedDefinitionBakeWriteKind.StaticDefinitionBlobCache:
                    staticBlobCacheRequests.Add(new GASGeneratedDefinitionStaticBlobCacheRequest(
                        write.DefinitionKind,
                        write.DefinitionCode,
                        write.WritePhase,
                        write.WriteTarget));
                    break;
                case GASGeneratedDefinitionBakeWriteKind.RuntimeArchetypeTemplate:
                    break;
                default:
                    skippedIneligibleWrites++;
                    break;
            }
        }
    }
}
