using System;
using System.Collections.Generic;
using GAS.Runtime;

namespace GAS.Editor
{
    public sealed class RowMetadata
    {
        public Type RowType { get; set; }

        public string DomainName { get; set; }

        public string CodeFieldName { get; set; }

        public GASDefinitionKind DefinitionKind { get; set; }

        public string BlobSchemaName { get; set; }

        public string LookupName { get; set; }

        public string BakerMethodName { get; set; }

        public string ComponentSetName { get; set; }

        public string QueryDescName { get; set; }

        public string CodeComponentType { get; set; }

        public string BlobComponentType { get; set; }

        public string RowFactoryTypeName { get; set; }

        public string RowFactoryMethodName { get; set; }

        public bool HasRowFactory => !string.IsNullOrWhiteSpace(RowFactoryTypeName)
                                     && !string.IsNullOrWhiteSpace(RowFactoryMethodName);

        public IReadOnlyList<BlobMemberInfo> BlobMembers { get; set; }

        public IReadOnlyList<RowValueSnapshot> RowValues { get; set; }
    }

    public sealed class RowValueSnapshot
    {
        public object Row { get; set; }

        public int Code { get; set; }
    }

    public sealed class BlobMemberInfo
    {
        public string Name { get; set; }

        public string BlobTypeName { get; set; }

        public string RowAccessor { get; set; }

        public bool IsBlobString { get; set; }

        public bool IsArray { get; set; }

        public bool RequiresAllocate { get; set; }

        public bool RequiresCast { get; set; }
    }
}
