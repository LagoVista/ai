using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using LagoVista.AI.Interfaces;
using LagoVista.AI.Rag.Chunkers.Models;

namespace LagoVista.AI.CloudRepos
{
    /// <summary>
    /// IDX-0052 – Azure Table Storage writer for resource usage records.
    ///
    /// Usage:
    ///  - Construct with storage account + key + table name.
    ///  - For each indexed file, call ReplaceUsagesForFileAsync(...)
    ///  - This writer will delete existing rows for (PartitionKey, RelativePath)
    ///    and upsert the new ones.
    /// </summary>
    public class ResourceUsageTableWriter : IResourceUsageTableWriter
    {
        private readonly TableClient _tableClient;

        public ResourceUsageTableWriter(string accountName, string accountKey, string tableName)
        {
            if (string.IsNullOrWhiteSpace(accountName)) throw new ArgumentNullException(nameof(accountName));
            if (string.IsNullOrWhiteSpace(accountKey)) throw new ArgumentNullException(nameof(accountKey));
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));

            var serviceUri = new Uri($"https://{accountName}.table.core.windows.net");
            var credential = new TableSharedKeyCredential(accountName, accountKey);

            _tableClient = new TableClient(serviceUri, tableName, credential);

            _tableClient.CreateIfNotExists();
        }

        public async Task ReplaceUsagesForFileAsync(
            IEnumerable<ResourceUsageRecord> records,
            CancellationToken token = default)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            var recordList = records.ToList();
            if (recordList.Count == 0)
            {
                return;
            }

            var groups = recordList
                .GroupBy(r => new
                {
                    PartitionKey = CreatePartitionKey(r),
                    RelativePath = NormalizePath(r.RelativePath)
                });

            foreach (var group in groups)
            {
                token.ThrowIfCancellationRequested();

                var partitionKey = group.Key.PartitionKey;
                var relativePath = group.Key.RelativePath;

                await DeleteUsagesForFileAsync(partitionKey, relativePath, token).ConfigureAwait(false);

                var entities = group.Select(ToEntity).ToList();
                await UpsertBatchAsync(entities, token).ConfigureAwait(false);
            }
        }

        private async Task DeleteUsagesForFileAsync(
            string partitionKey,
            string relativePath,
            CancellationToken token)
        {
            var filter = TableClient.CreateQueryFilter<ResourceUsageEntity>(
                e => e.PartitionKey == partitionKey && e.RelativePath == relativePath);

            var entitiesToDelete = _tableClient
                .QueryAsync<ResourceUsageEntity>(filter: filter, cancellationToken: token);

            var batch = new List<TableTransactionAction>(100);

            await foreach (var entity in entitiesToDelete.ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();

                batch.Add(new TableTransactionAction(TableTransactionActionType.Delete, entity));

                if (batch.Count == 100)
                {
                    await _tableClient.SubmitTransactionAsync(batch, token).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await _tableClient.SubmitTransactionAsync(batch, token).ConfigureAwait(false);
            }
        }

        private async Task UpsertBatchAsync(
            IReadOnlyList<ResourceUsageEntity> entities,
            CancellationToken token)
        {
            const int MaxBatchSize = 100;

            for (var offset = 0; offset < entities.Count; offset += MaxBatchSize)
            {
                token.ThrowIfCancellationRequested();

                var batchEntities = entities
                    .Skip(offset)
                    .Take(MaxBatchSize)
                    .ToList();

                if (batchEntities.Count == 0)
                {
                    break;
                }

                var actions = batchEntities
                    .Select(e => new TableTransactionAction(TableTransactionActionType.UpsertReplace, e))
                    .ToList();

                await _tableClient.SubmitTransactionAsync(actions, token).ConfigureAwait(false);
            }
        }

        private static ResourceUsageEntity ToEntity(ResourceUsageRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            var partitionKey = CreatePartitionKey(record);
            var rowKey = CreateRowKey(record);

            return new ResourceUsageEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,

                OrgId = record.OrgId,
                ProjectId = record.ProjectId,
                RepoId = record.RepoId,

                ResourceContainerFullName = record.ResourceContainerFullName,
                ResourceContainerShortName = record.ResourceContainerShortName,
                ResourceKey = record.ResourceKey,
                Culture = record.Culture,

                TargetModelFullName = record.TargetModelFullName,
                TargetModelPropertyName = record.TargetModelPropertyName,

                AttributeTypeName = record.AttributeTypeName,
                AttributePropertyName = record.AttributePropertyName,

                RelativePath = NormalizePath(record.RelativePath),
                SymbolName = record.SymbolName,
                SymbolFullName = record.SymbolFullName,
                SymbolKind = record.SymbolKind,
                SubKind = record.SubKind,

                UsageKind = (int)record.UsageKind,
                IsTestCode = record.IsTestCode,
                IsNameConstant = record.IsNameConstant,

                UsageContextSnippet = record.UsageContextSnippet,
                UsagePattern = record.UsagePattern
            };
        }

        private static string CreatePartitionKey(ResourceUsageRecord record)
        {
            var repoSafe = ToTableSafe(record.RepoId);
            var containerSafe = ToTableSafe(
                record.ResourceContainerFullName ??
                record.ResourceContainerShortName ??
                "UnknownContainer");

            return $"{repoSafe}|{containerSafe}";
        }

        private static string CreateRowKey(ResourceUsageRecord record)
        {
            var pathSafe = ToTableSafe(NormalizePath(record.RelativePath));
            var modelSafe = ToTableSafe(record.TargetModelFullName);
            var propSafe = ToTableSafe(record.TargetModelPropertyName);
            var keySafe = ToTableSafe(record.ResourceKey);

            return $"{pathSafe}|{modelSafe}|{propSafe}|{record.UsageKind}|{keySafe}";
        }

        private static string NormalizePath(string path) =>
            string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');

        private static string ToTableSafe(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }

            var trimmed = value.Trim();

            return trimmed
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace("#", "_")
                .Replace("?", "_");
        }
    }

    public class ResourceUsageEntity : ITableEntity
    {
        // ---------- Azure Table Storage Required Properties ----------

        /// <inheritdoc />
        public string PartitionKey { get; set; }

        /// <inheritdoc />
        public string RowKey { get; set; }

        /// <inheritdoc />
        public DateTimeOffset? Timestamp { get; set; }

        /// <inheritdoc />
        public ETag ETag { get; set; }

        // ---------- Identity ----------

        public string OrgId { get; set; }
        public string ProjectId { get; set; }
        public string RepoId { get; set; }

        // ---------- Resource Identity (soft FK to RESX) ----------

        public string ResourceContainerFullName { get; set; }
        public string ResourceContainerShortName { get; set; }
        public string ResourceKey { get; set; }
        public string Culture { get; set; }

        // ---------- Usage Target (Model / Property) ----------

        /// <summary>
        /// Fully qualified model/type name when the usage comes from a metadata
        /// attribute applied to a model or property.
        /// </summary>
        public string TargetModelFullName { get; set; }

        /// <summary>
        /// Property name when the usage comes from a metadata attribute on a
        /// specific property. Null/empty for type-level attributes.
        /// </summary>
        public string TargetModelPropertyName { get; set; }

        // ---------- Attribute Context ----------

        /// <summary>
        /// Attribute type name that carried the resource reference.
        /// </summary>
        public string AttributeTypeName { get; set; }

        /// <summary>
        /// Attribute property name that carried the resource reference.
        /// </summary>
        public string AttributePropertyName { get; set; }

        // ---------- SymbolName / File Location ----------

        public string RelativePath { get; set; }
        public string SymbolName { get; set; }
        public string SymbolFullName { get; set; }
        public string SymbolKind { get; set; }
        public string SubKind { get; set; }

        // ---------- Classification ----------

        /// <summary>
        /// Usage kind stored as int corresponding to <see cref="ResourceUsageKind"/>.
        /// </summary>
        public int UsageKind { get; set; }

        public bool IsTestCode { get; set; }
        public bool IsNameConstant { get; set; }

        // ---------- Trace / Diagnostics ----------

        public string UsageContextSnippet { get; set; }
        public string UsagePattern { get; set; }
    }
}
