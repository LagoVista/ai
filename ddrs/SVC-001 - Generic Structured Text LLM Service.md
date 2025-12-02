# SVC-001 — Generic Structured Text LLM Service

**ID:** SVC-001  
**Title:** Generic Structured Text LLM Service  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Applies to all "analyze / refine / generate from text with typed output" use cases across the LagoVista / Aptix ecosystem.

---

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-02 06:55 EST (UTC-05:00)

---

## 1. Purpose
Define a **single, reusable, generic service abstraction** for calling a large language model with:
- A **system-level instruction block** (how to behave), and  
- A **single text payload** (what to operate on),

and receiving a **typed result**:
- The caller supplies a **.NET type `TResult`** that represents the desired output shape.
- The service uses `TResult` to determine the structure of the JSON response and to deserialize it into a typed instance.
- `TResult` may be a **simple type** (e.g., `string`) or a complex POCO/record.

This service is registered once in DI and shared by multiple higher-level domain services.

---

## 2. High-Level Behavior
1. The service exposes a **generic operation**:
   > Instructions + text + TResult → InvokeResult<TResult>

2. The caller provides:
   - A **system prompt** (instructions),
   - A **single text payload**, and
   - A **result type `TResult`**.

3. The service:
   - Generates a structural contract (schema) based on `TResult`,
   - Ensures the LLM returns JSON matching that shape,
   - Deserializes it into an instance of `TResult`,
   - Returns it wrapped in `InvokeResult<TResult>`.

4. A non-generic variant exists for simple string scenarios:
   > Instructions + text → InvokeResult<string>

---

## 3. Inputs: System Prompt + One Text Payload
1. Every call provides **exactly two logical inputs**:
   - A **system prompt** describing *how* the model should behave.
   - A **single text payload** describing *what* the model should operate on.

2. The system prompt must be **aligned with the result type (`TResult`)**, meaning:
   - It conceptually describes the fields or structure implied by TResult.
   - It does not request data that contradicts or exceeds that shape.
   - It emphasizes purpose, semantics, and domain expectations.

3. The service supplies the **formal schema**, while the prompt provides the **semantic instructions**:
   - Schema → structural contract.  
   - Prompt → meaning and intent.

4. The text payload is:
   - One coherent block, 
   - Raw or structured text, 
   - Entirely caller-controlled.

5. Multi-turn or multi-chunk interactions are out of scope.

---

## 4. Service Responsibilities (LLM Interaction + Typed Mapping)
1. The service is the **single point** responsible for translating prompts + text + `TResult` into a typed LLM result.

2. It supplies the structural contract (schema) based on `TResult`, independent of the caller.

3. It hides all provider details and presents a provider-agnostic surface.

4. All results are returned as **InvokeResult<TResult>**:
   - Success → Fully populated instance of `TResult`.
   - Failure → Typed result is not returned; errors and messages are included.

5. The service enforces strict correctness:
   - Mismatched, malformed, or shape-incompatible outputs yield errors.
   - No best-effort or silent partial results.

6. Error handling is uniform and consistent.

7. The service contains **no domain logic**.

---

## 5. Domain Services as Thin Orchestrators
1. Domain services never call the LLM directly.
2. They:
   - Choose `TResult`,
   - Build the system prompt,
   - Construct the single text payload,
   - Call the generic service.

3. After receiving `InvokeResult<TResult>`, the domain service decides what to do (apply changes, fallbacks, etc.).

4. Domain orchestrators remain thin, declarative, and highly testable.

5. Generic vs. string variants:
   - Use the generic call when structured data is required.
   - Use the string call for simple text transformations.

---

## 6. V1 Scope and Boundary Conditions
1. **Single-turn only**.
2. **No tools** or function-calling semantics.
3. **No streaming**.
4. Always schema-driven and typed.
5. No orchestration logic such as retries or chained flows.
6. Future DDRs may extend capabilities; SVC-001 defines the stable foundation.

---

**End of SVC-001**
