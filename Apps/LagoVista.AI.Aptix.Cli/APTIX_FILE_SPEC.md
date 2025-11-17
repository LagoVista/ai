
# Aptix File Delivery Specification  
*(APTIX_FILE_SPEC.md)*

This specification defines how any Aptix-aware LLM agent must output file
modifications so that local tooling (CLI, VS Code extension, build tools) can
safely apply them.

It supports three delivery formats:

1. **Aptix File Bundle** – complete file replacements or new files
2. **Aptix Structured Patch** – JSON-based minimal patches
3. **Aptix Git Patch** – unified diff patches for `git apply`

The goal is a deterministic, lossless pipeline between the LLM and the user’s
local machine.

---

## 1. Aptix File Bundle

**Use when:**

- The user asks for entire files (e.g., “give me an Aptix file bundle”)
- The user is okay overwriting full files in the workspace
- Large refactors or new files are being introduced
- A patch is too risky or ambiguous

**Output format:**

- MUST be delivered inside a single fenced `json` code block in the chat UI,
  e.g.:

```json
{
  "root": ".",
  "files": [
    {
      "path": "relative/path/from/root.ext",
      "content": "<full file contents here>"
    }
  ]
}
```

- Inside the code block the content MUST be **pure JSON**:
  - No extra comments
  - No nested code fences
  - No explanatory text

### 1.1 JSON Structure

Top-level object:

- `root` (string)
  - The logical workspace root for paths, usually `"."`.

- `files` (array of FileEntry)
  - File operations to apply under `root`.

Each `FileEntry`:

- `path` (string, required)
  - Relative path from `root`, using forward slashes.
- `content` (string, optional but required for full-file create/replace)
  - The entire file content to write.
- `operation` (string, optional)
  - If omitted: default is “create or replace”.
  - If present, allowed values (design extensible):
    - `"create"` – create a new file; client MAY fail if it already exists.
    - `"replace"` – replace contents of an existing file (v1 behavior).
    - `"delete"` – delete the file; `content` MAY be null/omitted.
    - `"patch"` – structured patch (see section 2) – usually in a separate
      “structured patch bundle”.
    - `"gitPatch"` – Git patch file (see section 3).

### 1.2 Semantics (Current Minimum)

For current tools that only support full-file behavior:

- If `operation` is omitted **or** is `"create"` or `"replace"`:
  - Write `content` to `path`, creating or overwriting as needed.
- `delete`, `patch`, and `gitPatch` MAY be ignored by minimal clients.

### 1.3 When to Prefer File Bundles

- Creating new files (classes, components, configs).
- Large changes where patching is complex.
- When the user clearly says it’s safe to overwrite:
  - e.g., “Feel free to replace the whole file.”
- When the LLM cannot reliably compute a minimal patch.

---

## 2. Aptix Structured Patch (JSON Patch Bundle)

**Use when:**

- The user explicitly requests a patch (e.g., “aptix file patch”) and wants
  minimal changes.
- Only small, deterministic edits are required.
- The LLM has (or is given) enough context to compute literal matches.

**Important:** Structured patch mode is about **minimal edits**, not full file
replacement.

### 2.1 Output Format

The LLM MUST output a single fenced `json` code block containing a patch bundle,
for example:

```json
{
  "root": ".",
  "patches": [
    {
      "path": "src/MyFile.cs",
      "replacements": [
        {
          "find": "public void OldMethod()",
          "replace": "public void NewMethod()"
        },
        {
          "find": "_adminLogger.Trace(\"[OldTag]\")",
          "replace": "_adminLogger.Trace(\"[NewTag]\")"
        }
      ]
    }
  ]
}
```

### 2.2 JSON Structure

Top-level object:

- `root` (string)
  - Workspace root, usually `"."`.
- `patches` (array of PatchEntry)
  - Each describes patches for a single file.

Each `PatchEntry`:

- `path` (string, required)
  - Relative path of the file to patch.
- `replacements` (array of ReplacementEntry, required)
  - Literal text replacements in that file.

Each `ReplacementEntry`:

- `find` (string, required)
  - Literal substring to search for in the file content.
- `replace` (string, required)
  - Replacement text for the first match of `find`.
- `limit` (string, optional, default `"once"`)
  - `"once"` – replace first occurrence only (initial requirement).
  - `"all"` – future extension for “replace all occurrences”.

### 2.3 Semantics

For each `PatchEntry`:

1. Read the file at `root/path` as text.
2. For each `ReplacementEntry` in order:
   - Locate the first occurrence of `find` (for `limit = "once"`).
   - If found:
     - Replace with `replace`.
   - If not found:
     - Client MAY:
       - Log a warning, and skip that replacement, or
       - Fail the patch application.
3. Write the patched text back to disk.

Clients MAY initially implement only `"once"` behavior.

### 2.4 Safety Requirements (for LLMs)

When the user requests an **Aptix file patch**:

- The LLM MUST NOT rewrite entire files.
- The LLM MUST restrict itself to minimal `find`/`replace` operations.
- The LLM MUST be confident that `find` strings uniquely identify the intended
  locations.
- If there is **any ambiguity** or missing context, the LLM MUST reply:

> `A patch cannot be safely generated with the information provided.`

and NOT emit a patch bundle.

---

## 3. Aptix Git Patch (Unified Diff)

**Use when:**

- The user explicitly requests a Git-style patch, or
- Multi-file edits with fine-grained context are needed, and
- The environment has Git available to apply patches.

The Git patch format allows the user’s system to perform context-aware updates
with `git apply` or similar tools.

### 3.1 Output Format

The LLM MUST output a fenced `patch` code block for the Git patch content, e.g.:

```patch
diff --git a/src/Foo.cs b/src/Foo.cs
index 1234567..89abcde 100644
--- a/src/Foo.cs
+++ b/src/Foo.cs
@@ -10,7 +10,7 @@ public class Foo
 {
-    public int Count;
+    public int TotalCount;
 }
```

The patch content:

- MUST be a valid unified diff.
- MUST NOT contain commentary.
- MUST only contain diff metadata (`diff --git`, `index`, `---`, `+++`, `@@`)
  and the hunks themselves.

A client MAY choose to store this patch in a file such as
`.aptix/patches/aptix-change.patch` and run:

```bash
git apply .aptix/patches/aptix-change.patch
```

### 3.2 LLM Safety Rules for Git Patches

- If the LLM cannot reliably compute a correct diff, it MUST respond:

> `Unable to generate a safe Git patch; fallback to Aptix file bundle.`

- Under no circumstances should the LLM guess or synthesize incomplete/invalid
  diff content.

---

## 4. User Intent and Mode Selection

The LLM MUST respect explicit user requests:

### 4.1 When user requests **“Aptix file bundle”**

The LLM:

- MUST output a single `json` fenced block containing a valid **File Bundle**.
- MAY include multiple file entries.
- MAY replace entire files.
- MUST NOT attempt to be minimal unless requested.

Example user phrases:

- “Give me an Aptix file bundle for these changes.”
- “Return an Aptix file bundle that I can paste into my extension.”
- “It’s okay to overwrite the files, just give me the bundle.”

### 4.2 When user requests **“Aptix file patch”**

The LLM:

- MUST output either:
  - A **Structured Patch** JSON bundle (section 2), OR
  - A **Git Patch** (section 3), depending on the user request.
- MUST NOT supply full file contents.
- MUST aim for minimal, precise edits.

Example user phrases:

- “Provide an Aptix file patch only.”
- “I may have local changes, please give me just the patch.”
- “Generate a git patch I can apply locally.”

### 4.3 When user does NOT specify a mode

The LLM should infer:

- If the user talks about “minimal changes” or “patches” → prefer **Structured
  Patch** or **Git Patch**.
- If the user is okay with overwriting entire files → prefer **File Bundle**.
- If the change is large or complex → prefer **File Bundle**.

---

## 5. Mixed Responses (Advanced)

A single answer MAY include multiple fenced blocks, for example:

- One `json` block with a File Bundle.
- One `patch` block with a Git patch.

Example layout:

```text
### Aptix File Bundle
```json
{ "root": ".", "files": [ ... ] }
```

### Git Patch
```patch
diff --git ...
```
```

The client can:

1. Apply the File Bundle.
2. Apply the Git Patch.

However, **the LLM should avoid overlapping modifications** to the same file
via both mechanisms in the same response.

---

## 6. Error Handling Expectations

### 6.1 For Structured Patches

If a minimal patch is requested but the LLM cannot generate a safe patch:

- It MUST say, for example:

> `A patch cannot be safely generated with the information provided. Please allow a full Aptix file bundle instead.`

- It MUST NOT emit a partial or guessed patch.

### 6.2 For Git Patches

If Git patch generation is requested but not safe or not possible, the LLM MUST say:

> `Unable to generate a safe Git patch; fallback to Aptix file bundle or structured patch.`

---

## 7. Summary Table

| Mode                | Use Case                        | Format         | Fence Tag | Overwrite Allowed? |
|---------------------|---------------------------------|----------------|-----------|---------------------|
| **File Bundle**     | Full-file updates               | JSON object    | `json`    | Yes                 |
| **Structured Patch**| Minimal literal substitutions   | JSON object    | `json`    | No (patch only)     |
| **Git Patch**       | Git-native multi-file diffs     | Unified diff   | `patch`   | Managed by Git      |

---

## 8. Core Guarantees

Every Aptix-aware agent MUST:

- Always wrap outputs in the appropriate fenced code block (`json` or `patch`).
- Never mix commentary inside JSON or patch blocks.
- Declare explicitly when a patch cannot be safely generated.
- Prefer deterministic, idempotent file operations.
- Never overwrite user local changes in patch mode.
- Only overwrite entire files when using File Bundle mode with implied consent.

---

*End of APTIX_FILE_SPEC.md*
