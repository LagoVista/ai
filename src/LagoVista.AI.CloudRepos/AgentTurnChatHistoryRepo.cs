using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;
using LagoVista.AI.Models;
using System.Security.Cryptography;
using LagoVista.AI.CloudRepos;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.UserAdmin.Interfaces.Repos.Orgs;
using Azure.Storage;
using LagoVista.AI.Interfaces.Repos;


public sealed class AgentTurnRecordV1
{
    public int V { get; set; } = 1;

    public string OrgId { get; set; }
    public string SessionId { get; set; }
    public string TurnId { get; set; }

    public DateTimeOffset TsUtc { get; set; }

    public string UserInstructions { get; set; }
    public string ModelResponseText { get; set; }

    // Optional: store any extra metadata you may want later
    public Dictionary<string, string> Tags { get; set; }
}

public sealed class AgentTurnTranscriptBlobStore : IAgentTurnChatHistoryRepo
{
    // Placeholders: wire these up from configuration/DI

    private readonly BlobContainerClient _container;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AgentTurnTranscriptBlobStore(IMLRepoSettings settings)
    {
        var credential = new StorageSharedKeyCredential(settings.MLTableStorage.AccountId, settings.MLTableStorage.AccessKey);
        var serviceClient = new BlobServiceClient(new Uri($"https://{settings.MLTableStorage.AccountId}.blob.core.windows.net"), credential);

        _container = serviceClient.GetBlobContainerClient(nameof(AgentTurnChatHistory).ToLower());
    }

    public async Task AppendTurnAsync(
        string orgId,
        string sessionId,
        string turnId,
        string userInstructions,
        string modelResponseText,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentException("orgId required", nameof(orgId));
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId required", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(turnId)) throw new ArgumentException("turnId required", nameof(turnId));

        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var appendBlob = _container.GetAppendBlobClient(GetSessionBlobName(orgId, sessionId));

        // Ensure append blob exists
        await appendBlob.CreateIfNotExistsAsync(cancellationToken: ct);

        // Create one JSON line per turn
        var record = new AgentTurnRecordV1
        {
            V = 1,
            OrgId = orgId,
            SessionId = sessionId,
            TurnId = turnId,
            TsUtc = DateTimeOffset.UtcNow,
            UserInstructions = userInstructions ?? string.Empty,
            ModelResponseText = modelResponseText ?? string.Empty
        };

        var bytes = SerializeRecordJsonlWith2MbCap(record, orgId, sessionId, turnId);
        await AppendWithLeaseAsync(appendBlob, bytes, ct);
    }

    private const int MaxRecordBytes = 2 * 1024 * 1024; // 2 MiB cap for one JSONL line (UTF-8)

    private static string BuildTruncationDisclaimer(
        string fieldName,
        int originalUtf8Bytes,
        int storedUtf8Bytes,
        int capBytes,
        string orgId,
        string sessionId,
        string turnId)
    {
        return
    $@"[!!! TRUNCATED TURN FIELD !!!]
Field: {fieldName}
OrgId: {orgId}
SessionId: {sessionId}
TurnId: {turnId}
Original UTF-8 bytes: {originalUtf8Bytes}
Stored UTF-8 bytes (field only): {storedUtf8Bytes}
Record cap (bytes): {capBytes}
NOTE: Content was truncated client-side/server-side to fit storage limits.
--- ORIGINAL CONTENT CONTINUES (TRUNCATED) ---
";
    }

    private static string TruncateToUtf8ByteLimit(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var utf8 = Encoding.UTF8;

        // Quick check: if already within limit, return as-is
        int byteCount = utf8.GetByteCount(value);
        if (byteCount <= maxBytes)
            return value;

        // Encode and cut by bytes
        byte[] bytes = utf8.GetBytes(value);
        int len = Math.Min(maxBytes, bytes.Length);

        // Ensure we don't end in the middle of a UTF-8 sequence:
        // Move len backward until it is a valid UTF-8 boundary.
        // UTF-8 continuation bytes are 0b10xxxxxx (0x80..0xBF).
        while (len > 0 && (bytes[len - 1] & 0xC0) == 0x80)
            len--;

        // Also handle the case where we cut right after a leading byte that expects continuation bytes.
        // We'll validate by trying to decode; if it fails, back off a bit more.
        for (int backoff = 0; backoff < 4; backoff++)
        {
            try
            {
                return utf8.GetString(bytes, 0, len);
            }
            catch
            {
                len = Math.Max(0, len - 1);
                while (len > 0 && (bytes[len - 1] & 0xC0) == 0x80)
                    len--;
            }
        }

        // Worst case: return empty
        return string.Empty;
    }

    private static byte[] SerializeRecordJsonl(AgentTurnRecordV1 record)
    {
        var json = JsonSerializer.Serialize(record, JsonOptions);
        var line = json + "\n";
        return Encoding.UTF8.GetBytes(line);
    }

    private static byte[] SerializeRecordJsonlWith2MbCap(
        AgentTurnRecordV1 record,
        string orgId,
        string sessionId,
        string turnId)
    {
        // First attempt
        var bytes = SerializeRecordJsonl(record);
        if (bytes.Length <= MaxRecordBytes)
            return bytes;

        // Truncate ModelResponseText first
        bytes = TruncateFieldToFit(record, fieldName: nameof(record.ModelResponseText), orgId, sessionId, turnId);
        if (bytes.Length <= MaxRecordBytes)
            return bytes;

        // Then truncate UserInstructions if needed
        bytes = TruncateFieldToFit(record, fieldName: nameof(record.UserInstructions), orgId, sessionId, turnId);
        if (bytes.Length <= MaxRecordBytes)
            return bytes;

        // If we STILL don't fit, it means disclaimers + minimal JSON overhead are too big.
        // In practice this won't happen, but we can hard-minimize both fields.
        record.UserInstructions = "[!!! TRUNCATED !!!] (content removed to fit record cap)";
        record.ModelResponseText = "[!!! TRUNCATED !!!] (content removed to fit record cap)";
        bytes = SerializeRecordJsonl(record);

        // At this point it should fit; if not, something is very wrong (enormous tags, etc.)
        // We'll return it anyway; append will fail if it exceeds service limits.
        return bytes;
    }

    private static byte[] TruncateFieldToFit(
        AgentTurnRecordV1 record,
        string fieldName,
        string orgId,
        string sessionId,
        string turnId)
    {
        string currentValue = fieldName == nameof(record.ModelResponseText)
            ? record.ModelResponseText
            : record.UserInstructions;

        currentValue ??= string.Empty;

        // We'll iteratively reduce this field until the whole record fits.
        // Start by reserving some space for disclaimer + some content.
        var originalBytes = Encoding.UTF8.GetByteCount(currentValue);

        // If field is already empty, nothing to do.
        if (originalBytes == 0)
            return SerializeRecordJsonl(record);

        // Binary-search a byte limit for the field to make the record fit.
        int lo = 0;
        int hi = originalBytes;

        byte[] best = null;
        string bestField = null;

        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;

            var truncatedContent = TruncateToUtf8ByteLimit(currentValue, mid);
            var storedBytes = Encoding.UTF8.GetByteCount(truncatedContent);

            var disclaimer = BuildTruncationDisclaimer(fieldName, originalBytes, storedBytes, MaxRecordBytes, orgId, sessionId, turnId);
            var newValue = disclaimer + truncatedContent;

            if (fieldName == nameof(record.ModelResponseText))
                record.ModelResponseText = newValue;
            else
                record.UserInstructions = newValue;

            var candidate = SerializeRecordJsonl(record);

            if (candidate.Length <= MaxRecordBytes)
            {
                best = candidate;
                bestField = newValue;
                lo = mid + 1; // try to keep more content
            }
            else
            {
                hi = mid - 1; // too big, keep less
            }
        }

        // Restore best field value (so record stays consistent with returned bytes)
        if (bestField != null)
        {
            if (fieldName == nameof(record.ModelResponseText))
                record.ModelResponseText = bestField;
            else
                record.UserInstructions = bestField;

            return best;
        }

        // If nothing fits, wipe field to just disclaimer
        var minimalDisclaimer = BuildTruncationDisclaimer(fieldName, originalBytes, 0, MaxRecordBytes, orgId, sessionId, turnId);
        if (fieldName == nameof(record.ModelResponseText))
            record.ModelResponseText = minimalDisclaimer;
        else
            record.UserInstructions = minimalDisclaimer;

        return SerializeRecordJsonl(record);
    }

    public async Task<ListResponse<AgentTurnChatHistory>> RestoreSessionAsync(string orgId, string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentException("orgId required", nameof(orgId));
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId required", nameof(sessionId));

        var appendBlob = _container.GetAppendBlobClient(GetSessionBlobName(orgId, sessionId));

        // If blob doesn't exist, return empty session
        if (!await appendBlob.ExistsAsync(ct))
            return ListResponse<AgentTurnChatHistory>.Create(new List<AgentTurnChatHistory>());

        // One read: download the blob and parse JSONL
        var results = new List<AgentTurnChatHistory>();

        // Streaming download
        BlobDownloadStreamingResult download = await appendBlob.DownloadStreamingAsync(cancellationToken: ct);
        await using var contentStream = download.Content;

        using var reader = new StreamReader(contentStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: false);

        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            AgentTurnRecordV1 record;
            try
            {
                record = JsonSerializer.Deserialize<AgentTurnRecordV1>(line, JsonOptions);
            }
            catch (JsonException)
            {
                // If you want to be resilient to a corrupted last line, you can break here instead.
                // For strictness, throw.
                throw;
            }

            // Basic safety: ensure org/session match (avoid accidental cross-session restore)
            if (!string.Equals(record.OrgId, orgId, StringComparison.Ordinal) ||
                !string.Equals(record.SessionId, sessionId, StringComparison.Ordinal))
            {
                // You can choose to ignore or throw. Throwing is safer.
                throw new InvalidOperationException("Transcript record org/session mismatch.");
            }

            results.Add(new AgentTurnChatHistory
            {
                TsUtc = record.TsUtc,
                TurnId = record.TurnId,
                UserInstructions = record.UserInstructions,
                ModelResponseText = record.ModelResponseText
            });
        }

        return ListResponse< AgentTurnChatHistory>.Create( results);
    }

    private static string GetSessionBlobName(string orgId, string sessionId)
    {
        // Keep it deterministic and “folder-like”
        // Example: transcripts/org-123/session-456.jsonl
        return $"transcripts/{EscapePath(orgId)}/{EscapePath(sessionId)}.jsonl";
    }

    private static string EscapePath(string value)
    {
        // Minimal path safety. You can do more (e.g., base64url) if you expect odd characters.
        return value.Replace("\\", "_").Replace("/", "_").Trim();
    }

    private static async Task AppendWithLeaseAsync(AppendBlobClient appendBlob, byte[] bytes, CancellationToken ct)
    {
        const int maxAttempts = 5;
        var delay = TimeSpan.FromMilliseconds(150);

        // transactionalContentHash is MD5 for the specific append operation
        byte[] md5;
        using (var hasher = MD5.Create())
            md5 = hasher.ComputeHash(bytes);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            BlobLeaseClient leaseClient = appendBlob.GetBlobLeaseClient();
            Response<BlobLease> lease = null;

            try
            {
                lease = await leaseClient.AcquireAsync(TimeSpan.FromSeconds(15), cancellationToken: ct);

                var conditions = new AppendBlobRequestConditions
                {
                    LeaseId = lease.Value.LeaseId
                };

                using var ms = new MemoryStream(bytes, writable: false);

                // NOTE: This matches the overload your SDK is asking for:
                // AppendBlockAsync(Stream content, byte[] transactionalContentHash, AppendBlobRequestConditions conditions, ...)
                await appendBlob.AppendBlockAsync(
                    content: ms,
                    transactionalContentHash: md5,
                    conditions: conditions,
                    progressHandler: null,
                    cancellationToken: ct);

                await leaseClient.ReleaseAsync(cancellationToken: ct);
                return;
            }
            catch (RequestFailedException ex) when (
                ex.ErrorCode == BlobErrorCode.LeaseAlreadyPresent ||
                ex.ErrorCode == BlobErrorCode.LeaseIdMismatchWithBlobOperation ||
                ex.ErrorCode == BlobErrorCode.LeaseIsBreakingAndCannotBeAcquired)
            {
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            finally
            {
                if (lease != null)
                {
                    try
                    {
                        await appendBlob.GetBlobLeaseClient(lease.Value.LeaseId).ReleaseAsync(cancellationToken: ct);
                    }
                    catch
                    {
                        // best-effort
                    }
                }
            }
        }

        throw new Exception("Failed to append transcript after multiple attempts due to blob lease contention.");
    }
}