///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

#if UNITY_EDITOR
using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime.Generated
{
    public sealed class GASGeneratedDefinitionCatalogAuthoring : MonoBehaviour
    {
        public int Revision = GASGeneratedDefinitionCatalogInfo.SchemaVersion;
    }

    public sealed class GASGeneratedDefinitionCatalogBaker : Baker<GASGeneratedDefinitionCatalogAuthoring>
    {
        public override void Bake(GASGeneratedDefinitionCatalogAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var catalog = GASGeneratedDefinitionCatalogBuilder.BuildCatalog();
            AddBlobAsset(ref catalog, out _);
            AddComponent(entity, new GASDefinitionCatalogComponent
            {
                Catalog = catalog,
                Revision = authoring.Revision,
            });
        }
    }
}
#endif
