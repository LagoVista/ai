# SYS-004 — Aptix File & Patch Contract

**ID:** SYS-004  
**Title:** Aptix File & Patch Contract  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-02 15:45:00 EST (UTC-05:00)

---

## 1. Purpose

This DDR defines how Aptix agents must format file-editing responses for the NuvOS Aptix VS Code extension. Agents do not edit files directly; they emit structured JSON bundles that the extension applies. The goal is for agents to produce valid bundles 99.9% of the time.

SYS-004 works together with SYS-001 (Aptix Development Workflow), which governs how DDRs are created, reviewed, approved, and bundled.

---

## 2. Invocation Modes

### 2.1 Invocation phrases

Agents MUST treat these phrases as hard mode selectors:

- **"aptix file bundle"** → agent MUST respond with an Aptix File Bundle (top-level `files`).
- **"aptix file patch"** → agent MUST respond with a Structured Patch Bundle (top-level `patches`).

If both appear or intent is unclear, the agent MUST either:

- Ask for clarification, or
- Choose the more restrictive mode (`aptix file patch`) and respond in natural language without JSON.

If neither phrase is present, the agent MAY respond in natural language only, or MAY produce a file bundle when the user clearly requests a machine-applicable bundle.

### 2.2 Mode exclusivity

Agents MUST NOT mix modes within a single response:

- File Bundle → top-level `files`
- Structured Patch Bundle → top-level `patches`

Exactly one of these MUST appear in any JSON response.

---

## 3. File Bundle JSON Shape and Semantics

When the user requests **"aptix file bundle"**, the agent MUST emit a JSON object of this shape, using multiline pretty formatting:

```json
{
  "root": ".",
  "files": [
    {
      "path": "relative/path.ext",
      "operation": "create | replace | delete | patch | gitPatch",
      "content": "...optional...",
      "patches": [
        {
          "find": "...",
          "replace": "...",
          "limit": "once | all"
        }
      ]
    }
  ]
}
```

### 3.1 Root

- MUST be present
- MUST be a string
- For backend repos, MUST be "." unless explicitly overridden
- All paths MUST be relative to `root`

### 3.1.1 Root selection rules

- Default: For backend repos, `root` MUST be `"."`.
- Agents MAY use `root: "./src"` ONLY when **all** file paths in the bundle are under `src/`.
- If **any** file path is under `tests/` (or the bundle mixes `src/` and `tests/`), `root` MUST be `"."`.
- If the user explicitly requests a different root, the agent MUST comply ONLY if all paths remain valid under SYS-004 path rules (no `../`, no `./` prefixes).


### 3.2 `files[]` rules

Each `files[]` entry MUST contain:

- `path` — relative path, POSIX separators (`/`), no leading `./`, no `../`
- `operation` — one of:
  - `create`
  - `replace`
  - `delete`
  - `patch`
  - `gitPatch`

Optional:

- `content` — required for some operations
- `patches` — required for `operation: "patch"`

### 3.3 Operation requirements

- **create** → MUST include `content`, MUST NOT include `patches`
- **replace** → MUST include `content`, MUST NOT include `patches`
- **delete** → MUST NOT include `content` or `patches`
- **patch** → MUST include `patches` array; `content` SHOULD NOT appear
- **gitPatch** → MUST include unified diff in `content`; MUST NOT include `patches`

### 3.4 Patches inside file bundles

- `patches` MUST be nested in a `files[]` entry
- MUST NOT appear at top level
- Each patch MUST contain:
  - `find`
  - `replace`
  - `limit: "once" | "all"`
- If `find` is not found, patch becomes **no-op**

---

## 4. Structured Patch Bundles ("aptix file patch")

When the user requests **"aptix file patch"**, the agent MUST emit minimal literal patches in this canonical shape:

```json
{
  "root": ".",
  "patches": [
    {
      "path": "relative/path.ext",
      "find": "...",
      "replace": "...",
      "limit": "once | all"
    }
  ]
}
```

Agents MUST NOT:

- Create new files
- Delete files
- Replace entire files

If a safe patch cannot be produced, the agent MUST NOT emit JSON and MUST respond in natural language explaining that an Aptix File Patch is not safe for this request.

---

## 5. Path Rules and Repository Layout

### 5.1 Allowed top-level directories

Agents MUST assume only the following exist:

- `./ddrs`
- `./ddrs/jsonl`
- `./src`
- `./tests`

Agents MUST NOT assume other top-level folders.

### 5.2 Path syntax

Agents MUST:

- Use POSIX separators (`/`)
- Avoid `./` prefixes
- Avoid `../` traversal
- Keep all paths relative to `root`
- Agents MUST NOT choose a `root` that would require any file path to use `../` to reach its target directory.


### 5.3 Selecting paths under `src` and `tests`

When editing an existing file:

- Preserve its known path exactly.

When creating a new file, agents MUST infer location using this precedence:

1. **Namespace match** → place file with other files of same namespace
2. **Sibling inference** → place next to related files
3. **Project-root inference** → use `src/{Project}/...` when clear
4. **Uncertain** → ask the user; agent MUST NOT guess

Tests SHOULD go under:

- `tests/{ProjectName}.Tests/`  
- File names SHOULD end with `Tests.cs`

### 5.4 DDR asset placement

- Markdown DDRs → `ddrs/{ID} - {Title}.md`
- LLM-optimized JSONL → `ddrs/jsonl/{ID}.jsonl`
- MUST NOT place DDR assets under src/ (e.g., src/ddrs/...).

### 5.5 Partial classes

Agents SHOULD split large or complicated C# types into multiple partial files.

All partials MUST:

- Use same namespace
- Share class name
- Live in same directory

---

## 6. JSON Validation & Escaping Rules

Agents MUST:

- Construct JSON as an internal object first
- Serialize it
- Validate that serialization is valid JSON (syntactically correct)

Agents MUST:

- Escape `"` as `\"`
- Escape `\` as `\\` where necessary
- Represent newlines as `\n`
- Avoid raw control characters
- Avoid single-line JSON entirely

If JSON cannot be safely constructed:

- Agent MUST NOT emit broken JSON
- Agent MUST reply in natural language and request a narrower or simplified change

---

## 7. JSON Formatting and Content Serialization Rules

Agents MUST adhere to the following formatting rules when emitting Aptix File Bundles or Patch Bundles.

### 7.1 Pretty-Printed JSON Structure

All emitted JSON MUST:

1. Be **pretty-printed**, with one field per line.
2. Use consistent indentation throughout the object.
3. Place `{` and `}` on their own lines.
4. Avoid single-line JSON objects entirely.
5. Match the general formatting style used in this DDR.

### 7.2 JSON String Encoding Requirements

Agents MUST:

1. Convert all newline characters within JSON string values to the escaped form `\n`.
2. Escape all double quotes as `\"`.
3. Escape all backslashes as `\\`.
4. Escape tabs as `\t`.
5. Escape any additional characters required by JSON string literal rules.
6. Avoid unescaped control characters (0x00–0x1F) under all circumstances.

### 7.3 Content Field Serialization

Because file contents are transported inside JSON, agents MUST:

1. Encode every `"content"` field as **one single JSON string literal**.
2. Ensure `"content"` contains **no raw newlines**—all newlines MUST be represented as `\n`.
3. Serialize content **after** constructing a normal multiline string internally.
4. Emit only the escaped one-line JSON string in `"content"`.

Agents MUST NOT:

- Emit raw multiline content in the `"content"` field.
- Emit arrays of lines or alternative encodings.
- Hand-escape text manually; correct escaping MUST come from JSON serialization.

### 7.4 Error Handling

If the agent cannot guarantee safe JSON formatting or escaping, it MUST NOT emit JSON.  
Instead, it MUST respond in natural language and request clarification or constraints.

---

## 8. Guarded Code Blocks & Syntax Highlighting

When emitting bundles, agents MUST:

- Use a single guarded code block
- Include only the JSON object inside it
- Prefer `json` as the language identifier when possible

---

## 9. Minimal Patch Rules & Safety

Agents MUST:

- Make patches as small as possible
- Preserve unrelated code
- Use literal `find` strings extracted directly from known file content whenever possible
- Order patches carefully when multiple patches affect same file

Agents MUST refuse to emit patches when:

- The change is structural or large
- No stable `find` anchor exists
- The user requested `aptix file patch` but the change is not patch-safe

---

## 10. Agent Checklist (Execution Flow)

### Step 0 — Decide if JSON is needed

- If not explicitly or implicitly requested → respond in natural language

### Step 1 — Select mode

- "aptix file bundle" → File Bundle Mode
- "aptix file patch" → Patch Mode
- Ambiguous → ask user

### Step 2 — Check safety

- If large/structural and Patch Mode → refuse (no JSON)

### Step 3 — Build operations & paths

- Choose `root`:
  - If bundle touches `tests/` OR mixes `src/` and `tests/` → `root: "."`
  - Else (src-only) → `root: "./src"` MAY be used if desired

### Step 4 — Construct JSON bundle object

- Ensure correct shape
- Ensure minimal operations

### Step 5 — Validate patches

- Minimal, safe, stable anchors

### Step 6 — JSON validation

- Serialize and ensure valid JSON formatting

### Step 7 — Emit

- Pretty formatted multiline JSON
- Single guarded code block
- Prefer `json` language identifier

---

This DDR is now formally approved and ready for indexing and long-term agent adherence.
