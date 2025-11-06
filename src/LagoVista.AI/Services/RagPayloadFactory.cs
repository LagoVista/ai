//using LagoVista.Core.Utils.Types.Nuviot.RagIndexing;
//using LagoVista.Core.Utils.Types;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Text;

//namespace LagoVista.AI.Services
//{
//    // Matches your plan:
//    // public sealed class RagChunkPlan { public string DocId; public IReadOnlyList<RagChunk> Chunks; public RawArtifact Raw; }

//    public sealed class IngestContext
//    {
//        public string OrgId { get; set; }          // from AgentContext.OwnerOrganization.Id
//        public string ProjectId { get; set; }      // e.g., agent context key or project
//        public string EmbeddingModel { get; set; } = "text-embedding-3-large";
//        public int IndexVersion { get; set; } = 1;
//    }

//    public sealed class PayloadBuildResult
//    {
//        public string PointId { get; set; }
//        public RagVectorPayload Payload { get; set; }
//        public string TextForEmbedding { get; set; }  // normalized chunk text
//        public int EstimatedTokens { get; set; }      // coarse estimate
//    }

//    public static class RagPayloadFactory
//    {
//        public static IReadOnlyList<PayloadBuildResult> FromPlan(
//            IRagIndexable doc,
//            RagChunkPlan plan,
//            IngestContext ctx)
//        {
//            if (doc == null) throw new ArgumentNullException(nameof(doc));
//            if (plan == null) throw new ArgumentNullException(nameof(plan));
//            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

//            var results = new List<PayloadBuildResult>(plan.Chunks?.Count ?? 0);

//            foreach (var c in plan.Chunks ?? Enumerable.Empty<RagChunk>())
//            {
//                var pointId = RagVectorPayload.BuildPointId(plan.DocId, c.SectionKey, c.PartIndex);
//                var text = c.TextNormalized ?? string.Empty;

//                var payload = new RagVectorPayload
//                {
//                    // Identity
//                    OrgId = ctx.OrgId,
//                    ProjectId = ctx.ProjectId,
//                    DocId = plan.DocId,

//                    // Classification
//                    ContentType = GuessContentType(doc.ContentSubtype),
//                    Subtype = doc.ContentSubtype,

//                    // Sectioning
//                    SectionKey = c.SectionKey,
//                    PartIndex = c.PartIndex,
//                    PartTotal = c.PartTotal,

//                    // Routing/meta from IRagIndexable
//                    Title = string.IsNullOrWhiteSpace(c.Title) ? null : c.Title,
//                    Language = doc.Language,
//                    Priority = doc.Priority,
//                    Audience = doc.Audience,
//                    Persona = doc.Persona,
//                    Stage = doc.Stage,
//                    LabelSlugs = doc.GetLabelSlugs()?.ToList() ?? new List<string>(),

//                    // Raw/provenance from plan.Raw + pointers from chunk
//                    BlobUri = plan.Raw != null ? plan.Raw.SuggestedBlobPath : null,
//                    SourceSha256 = plan.Raw != null ? plan.Raw.SourceSha256 : null,
//                    LineStart = c.LineStart,
//                    LineEnd = c.LineEnd,
//                    CharStart = c.CharStart,
//                    CharEnd = c.CharEnd,

//                    // Index/embedding governance
//                    IndexVersion = ctx.IndexVersion,
//                    EmbeddingModel = ctx.EmbeddingModel,
//                    ContentHash = Sha256(text),
//                    ContentLenChars = text.Length,
//                    // Optional to fill later if you have exact numbers:
//                    // ChunkSizeTokens = ...,
//                    // OverlapTokens   = ...,
//                    IndexedUtc = DateTime.UtcNow
//                };

//                // Validate now (optional but useful to fail fast)
//                var errs = RagVectorPayloadValidator.Validate(payload);
//                if (errs.Count > 0)
//                {
//                    throw new InvalidOperationException(
//                        "Invalid vector payload for " + pointId + ": " + string.Join("; ", errs));
//                }

//                results.Add(new PayloadBuildResult
//                {
//                    PointId = pointId,
//                    Payload = payload,
//                    TextForEmbedding = text,
//                    EstimatedTokens = EstimateTokens(text) // coarse ≈4 chars/token
//                });
//            }

//            return results;
//        }

//        private static RagContentType GuessContentType(string subtype)
//        {
//            if (string.IsNullOrWhiteSpace(subtype)) return RagContentType.Unknown;
//            var s = subtype.ToLowerInvariant();
//            if (s.Contains("csharp") || s.Contains("typescript") || s.Contains("code")) return RagContentType.Code;
//            return RagContentType.DomainDocument;
//        }

//        private static string Sha256(string text)
//        {
//            using (var sha = SHA256.Create())
//            {
//                var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
//                var hash = sha.ComputeHash(bytes);
//                var sb = new StringBuilder(hash.Length * 2);
//                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
//                return sb.ToString();
//            }
//        }

//        private static int EstimateTokens(string s)
//        {
//            if (string.IsNullOrEmpty(s)) return 0;
//            var extra = 0;
//            for (int i = 0; i < s.Length; i++)
//            {
//                char c = s[i];
//                if (c == '\n' || c == '\r') extra++;
//            }
//            return (int)Math.Ceiling((s.Length + extra) / 4.0);
//        }
//    }
//}
