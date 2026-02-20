# Claude's assignement

## My high-level ask

Read the AgentContext file the root of the Lib project. I would like to start creating a pipeline for developing new
features in this application. The goal for this is for me to operate in Claude Code. Claude code will act as the lead 
agent, with me as his customer. It is most important that I be able to re-use this pipeline through the development of
many new features while also retaining the ability to improve the process as we go. I am asking you here to provide 
prompts for me to use in future to set this up successfully and according to all my goals.


## Agents and roles

The lead agent should manage agents who can perform the following duties:
- read / analyze business requiremets documents (BRD) that I have written and saved into a directory in this project
- work with me to refine BRDs in cases where ambiguities arise through back-and-forth dialog in Claude Code, with Claude acting as the scribe to update the BRD
- write functional specifications and/or design documentation (FSD) with traceability back to the BRD
- work with me to refine designs per my overall technology strategy for this application through back-and-forth dialog in Claude Code, with Claude acting as the scribe to update the FSD
- update test cases as required and update the BusinessOutcomes.md file in Lib.Tests, including traceability back to the BRD or FSD
- write unit and regression tests that meet needs of the test cases, with traceability back to the test cases
- develop the code changes to the BRD, the FSD, and the unit / regression tests. 

## Key goals and objectives

- Quick or efficient SDLC is not the priority. Rather, the focus should be reliable software that meets my business intent.
- Where that intent is uncertain or ambiguous, ask questions. Do not proceed to the next step in SDLC without getting clarity from me. Don't ever just assume you understand
- standardize output structure. I believe Markdown is an excellent format, but I especially want every BRD to use the same structure. Same with FSDs and test cases.
- I need you to help me put up guardrails against hallucination
- I want to instill traceability to requirements through the process. They don't have to be in static documents, but I want to make Claude's job easier in future when I ask for new features
- Tightly constrain each agent’s role. It is my experience that tightly focused agents perform more accurately than an agent trying to do too much
- Prevent premature coding, meaning that SDLC should not progress into the next phase without arriving at certainty (with evidence supporting that certainty in the BRD, FSD, or test cases) that we understand the ask and desired outcome completely
- Force explicit reasoning and traceability, such that, if this process degrades, we can understand which step needs course correction.


# Claude's answer

## Why the original answer was wrong

The original response described a *fictional* orchestration system: one Claude instance
role-playing as multiple agents. That is not how Claude Code multi-agent actually works,
and it does not deliver the isolation, parallelism, or context-window protection that
make a real agent team valuable.

---

## How Claude Code Multi-Agent Actually Works

**You do not need a "lead agent initialization prompt."**
The Claude Code session you are already in IS the lead agent. It manages the conversation
with you, makes all gate decisions, and spawns sub-agents when needed.

Sub-agents are real, separate processes launched by the `Task` tool. Each sub-agent:
- Runs in its own isolated context window (protecting the main session from bloat)
- Receives its full context through the Task prompt — it cannot see the main chat
- Writes its output to a file on disk, then exits
- Reports a summary back to the lead (main session), which you then review

**Files on disk are the only shared memory between agents.**
The lead agent writes a handoff file before spawning a sub-agent; the sub-agent reads it,
does its work, writes its output, and the lead agent reads that output. This is the
mechanism. Everything else is just prompting discipline.

**Phase gates are real pauses in the main chat.**
After a sub-agent finishes, the lead agent presents its output to you in the main
conversation. You approve, request changes, or ask questions. Only after you explicitly
say "proceed" does the lead agent spawn the next phase's sub-agent. This is the only
reliable way to enforce "prevent premature coding."

---

## Directory Structure

```
PersonalFinance/
  ProjectDocuments/
    BRDs/                    ← you write these before starting a session
    FSDs/                    ← FSD sub-agent writes these; you review
    Pipeline/                ← agent-to-agent handoff files (scratch space)
      [FEATURE_ID]-status.md     ← phase tracker; survives session end (see below)
      [FEATURE_ID]-brd-analysis.md
      [FEATURE_ID]-impl-plan.md
  Lib.Tests/
    TestStrategy/
      BusinessOutcomes.md    ← test planning sub-agent updates this
      GapAnalysis.md         ← generated after test planning
```

---

## Pipeline Status Tracking

Every phase gate writes a status file to `ProjectDocuments/Pipeline/[FEATURE_ID]-status.md`.
This file is the single source of truth for where the pipeline stands. Because it lives on
disk, it survives session ends, context resets, and multi-day interruptions.

**Format:**
```markdown
# Pipeline Status: [FEATURE_ID]

BRD: [BRD_FILE_PATH]
Last updated: YYYY-MM-DD HH:MM

| Phase | Name              | Status      | Artifact                                      |
|-------|-------------------|-------------|-----------------------------------------------|
| 0     | BRD Analysis      | COMPLETE    | Pipeline/[FEATURE_ID]-brd-analysis.md         |
| 1     | FSD               | COMPLETE    | FSDs/[FEATURE_ID]-fsd.md                      |
| 2     | Test Planning     | IN PROGRESS | Lib.Tests/…/[FeatureName]Tests.cs             |
| 3     | Implementation    | NOT STARTED |                                               |
```

**Rule:** The lead agent updates this file at every phase gate — before asking you
whether to proceed. That way, if you close the session mid-question, the status file
already reflects what was completed.

---

## Standardized Document Templates

### BRD (you write this; no agent writes it for you)

```markdown
# BRD-NNN: [Feature Title]

**Author:** Dan
**Date:** YYYY-MM-DD
**Status:** Draft | In Review | Approved

## Background
Why this feature is needed. What problem it solves.

## Business Requirements
| ID      | Requirement                        | Acceptance Criteria                  |
|---------|------------------------------------|--------------------------------------|
| BRD-001 | [What the system must do]          | [How we know it is done correctly]   |
| BRD-002 | ...                                | ...                                  |

## Out of Scope
Explicit list of things this BRD does NOT cover.

## Open Questions
Questions that must be answered before FSD can be written.
```

### FSD (FSD sub-agent writes this)

```markdown
# FSD-NNN: [Feature Title]

**BRD:** BRD-NNN
**Author:** Claude Code (FSD Agent)
**Date:** YYYY-MM-DD
**Status:** Draft | In Review | Approved

## Design Summary
One paragraph: what this feature does and how it fits into the existing system.

## Functional Specifications
| ID      | BRD Trace | Description                  | Technical Approach              |
|---------|-----------|------------------------------|---------------------------------|
| FSD-001 | BRD-001   | [What the code must do]      | [Class/method/pattern to use]   |
| FSD-002 | BRD-002   | ...                          | ...                             |

## Data Model Changes
Table/column changes, new EF entities, migration notes.

## Method / API Changes
New or modified static methods, signatures, return types.

## Out of Scope
Explicitly excluded from this design.

## Unresolved Design Decisions
Items that require Dan's input before implementation begins.
```

### Test Case additions to BusinessOutcomes.md

Each new row added by the test planning sub-agent must follow this format:
```
| [Outcome description]  | C | Traces to FSD-NNN / BRD-NNN. No existing test. |
```
After agent writes tests, the row becomes:
```
| [Outcome description]  | A | TestClassName: `MethodName` |
```

---

## The Pipeline — Phase by Phase

> The lead agent (your Claude Code session) runs this. You do not paste anything special
> to start it. Just say: **"Start the SDLC pipeline for the BRD at [path]."**

---

### Phase 0 — BRD Analysis and Clarification

**Trigger:** You give the lead agent a BRD file path.

**What the lead agent does:**
1. Reads AgentContext.txt and the BRD itself.
2. Spawns an **Explore sub-agent** with this Task prompt:

```
You are a requirements analyst. Your only job is to analyze a BRD and produce a
structured analysis report. Do NOT suggest solutions or write code.

Read these files in full before writing anything:
  - /media/dan/fdrive/codeprojects/PersonalFinance/Lib/AgentContext.txt
  - [BRD_FILE_PATH]

Then search the codebase to verify your understanding of referenced concepts:
  - For every domain term in the BRD (e.g. "LONG_TERM position", "TaxLedger",
    "WithdrawalStrategy"), find the corresponding C# class or constant and cite
    the file path and line number.

Write your analysis to: ProjectDocuments/Pipeline/[FEATURE_ID]-brd-analysis.md

Structure that file as follows:
## Summary
One paragraph: what this feature does in plain English.

## Requirement-by-Requirement Analysis
For each BRD-NNN row:
  - BRD-NNN: [requirement text]
  - Codebase evidence: [file:line — what you found]
  - Clarity: CLEAR | AMBIGUOUS | MISSING_INFO
  - If AMBIGUOUS or MISSING_INFO: state the exact question that must be answered.

## Risks and Dependencies
Any existing code patterns that may conflict or constrain the design.

## Unanswered Questions
Numbered list of every question that must be resolved before FSD can be written.

Do not guess or infer. If you cannot find codebase evidence for a concept, say so
explicitly rather than assuming.
```

3. Reads the analysis report.
4. **Immediately** writes all findings and questions into the BRD's "Open Questions" section
   **before presenting anything to Dan.** This ensures the document has clearly stated loose ends
   even if the session ends before Dan can respond. Format:
   - Each CLEAR finding is recorded as a one-line note (e.g., "BR-4 finding: no code change needed — debt payments are already fixed. ✓ RESOLVED")
   - Each AMBIGUOUS or MISSING_INFO item becomes a numbered question with a `Dan's answer: ___` placeholder
   - Questions are grouped by BR number
5. Presents every open question to you in the main chat (drawn from the BRD, which is now the
   source of truth).
6. Acts as scribe: fills in each `Dan's answer: ___` placeholder in the BRD as you respond.
7. Repeats until all placeholders are filled.

**Loose-ends rule:** At no point should an unanswered question exist only in the chat transcript.
Every open question must be written to either the BRD or the brd-analysis.md artifact before
being presented to Dan. The pipeline document and chat are ephemeral; the files on disk are not.

**Phase gate:** Lead agent updates `Pipeline/[FEATURE_ID]-status.md` (Phase 0 → COMPLETE),
then asks:
> "All BRD questions are resolved. The updated BRD is at [path]. Shall I proceed to FSD?"

You say yes or request further BRD changes.

---

### Phase 1 — Functional Specification Design

**Trigger:** Your approval to proceed from Phase 0.

**What the lead agent does:**
1. Spawns a **Plan sub-agent** with this Task prompt:

```
You are a functional specification writer for a C# .NET 8.0 financial simulation.
Your only job is to write a FSD. Do NOT write any C# code.

Read these files in full before writing anything:
  - /media/dan/fdrive/codeprojects/PersonalFinance/Lib/AgentContext.txt
  - [BRD_FILE_PATH]  (approved BRD — all questions are resolved)
  - ProjectDocuments/Pipeline/[FEATURE_ID]-brd-analysis.md

Constraints on your design:
  - Follow the functional/immutable patterns described in AgentContext.txt:
    static classes, copy-and-return, tuple returns, no business logic in
    data-holding classes.
  - Every FSD-NNN item must reference the BRD-NNN it satisfies.
  - Do not invent requirements not present in the BRD.
  - For every proposed new method or class, verify the naming convention by
    reading the most similar existing file in the codebase and matching it.
  - Flag any design decision that requires Dan's input as "OPEN DECISION."

Write the FSD to: ProjectDocuments/FSDs/[FEATURE_ID]-fsd.md

Use the FSD template from ProjectDocuments/ClaudePrompt_0_how_do_I_set_this_up.md.
```

2. Reads the FSD.
3. **Immediately** writes all OPEN DECISION items into the FSD's "Unresolved Design Decisions"
   section **before presenting anything to Dan**, each with a `Dan's decision: ___` placeholder.
   The FSD is the source of truth; the chat is not.
4. Presents a summary and all OPEN DECISION items to you (drawn from the FSD).
5. Acts as scribe: fills in each `Dan's decision: ___` placeholder in the FSD as you respond.
6. Repeats until all placeholders are filled.

**Loose-ends rule:** Every open design decision must be written to the FSD before being presented
to Dan. A decision that exists only in the chat transcript is a lost loose end.

**Phase gate:** Lead agent updates `Pipeline/[FEATURE_ID]-status.md` (Phase 1 → COMPLETE),
then asks:
> "FSD is complete with no open decisions. Summary: [N] functional specs covering
> BRD-001 through BRD-NNN. Shall I proceed to test planning?"

---

### Phase 2 — Test Planning and Test Writing

**Trigger:** Your approval to proceed from Phase 1.

**What the lead agent does:**
1. Spawns a **general-purpose sub-agent** with this Task prompt:

```
You are a test planner for a C# .NET 8.0 financial simulation. Your job has
three parts: (1) write a standalone test strategy document, (2) update
BusinessOutcomes.md with new outcome rows, and (3) write full xUnit tests.
Do NOT write any implementation code.

Read these files in full before writing anything:
  - /media/dan/fdrive/codeprojects/PersonalFinance/Lib/AgentContext.txt
  - [BRD_FILE_PATH]
  - ProjectDocuments/FSDs/[FEATURE_ID]-fsd.md
  - Lib.Tests/TestStrategy/BusinessOutcomes.md
  - Lib.Tests/TestDataManager.cs  (to know what test helpers exist)
  - [2-3 existing test files most similar to what this feature needs]

--- PART 1: Test Strategy Document ---

Write a standalone test strategy document to:
  ProjectDocuments/Pipeline/[FEATURE_ID]-test-strategy.md

Structure it as follows:
  ## What This Feature Does (Test Perspective)
  One paragraph on the core invariant being tested.

  ## Scope
  In scope / Out of scope subsections.

  ## Test Groups and Rationale
  One subsection per logical test group. For each group:
    - Why this group of behavior must be tested
    - Key scenarios covered
    - Edge cases and their rationale
    - Any confirmed no-ops and why they need no test

  ## Pre-Implementation Notes
  Document any method signatures that do not yet exist (because implementation
  is not written), so the implementer has a clear checklist.

  ## BusinessOutcomes.md Coverage
  Summary of rows added and their status.

--- PART 2: Update BusinessOutcomes.md ---

Add a new numbered section (e.g., ## 16. Feature Name) to BusinessOutcomes.md.
For each FSD-NNN item:
  - Add a row with status C and FSD-NNN / BR-N trace in the Notes column.
  - For confirmed no-ops (where the FSD documents that no code change is needed),
    add a note row explaining the no-op rather than leaving a gap.

--- PART 3: Write the test file(s) ---

Write tests to: Lib.Tests/MonteCarlo/StaticFunctions/[FeatureName]Tests.cs

Rules:
  - Namespace: Lib.Tests.MonteCarlo.StaticFunctions
  - Use [Fact(DisplayName = "§X.Y — description")] for traceability
  - Write the full test body with realistic assertions — not stubs
  - Since implementation is not written yet, ALL tests must be marked:
    [Fact(Skip = "Not yet implemented — FSD-NNN", DisplayName = "§X.Y — description")]
  - Include a file-level summary comment listing every method that does not yet
    have its new signature, with the current file:line reference
  - Use decimal literals with m suffix; use NodaTime.LocalDateTime for dates
  - Do NOT use TestDataManager.CreateTestPerson() for age-sensitive tests —
    create PgPerson inline with a fixed BirthDate

After writing tests, update each BusinessOutcomes.md row you added from C → A,
citing the test class name and §X.Y section number.

--- PART 4: Build verification ---

Run:
  dotnet build Lib.Tests/Lib.Tests.csproj --no-restore -v q
Fix any errors. If new method signatures do not yet exist, comment out the
call in the test body and replace with Assert.True(false, "Not yet implemented — FSD-NNN").

Report: total tests, new tests added, skipped count, build status.
```

2. Reads the test file, test strategy doc, and BusinessOutcomes.md changes.
3. **Immediately** writes any coverage gaps or unresolved test design questions as:
   - A row with status `?` in BusinessOutcomes.md, AND
   - A note in the test strategy doc's "Pre-Implementation Notes" section
   **before presenting anything to Dan.**
4. Presents coverage summary to Dan: FSD items covered, any gaps, skipped count.
5. Acts as scribe: resolves gap rows in BusinessOutcomes.md and updates test strategy doc
   as Dan provides direction.

**Loose-ends rule:** Any coverage gap or ambiguity must appear in both BusinessOutcomes.md
and the test strategy doc before being raised in chat. Do not describe gaps only verbally.

**Phase gate:** Lead agent updates `Pipeline/[FEATURE_ID]-status.md` (Phase 2 → COMPLETE),
updating the artifact column to list both files:
```
| 2 | Test Planning | COMPLETE | Pipeline/[FEATURE_ID]-test-strategy.md + Lib.Tests/.../[Name]Tests.cs |
```
Then asks:
> "[N] test cases written covering all FSD items. Build passes. [M] tests are
> skipped pending implementation. Test strategy at Pipeline/[FEATURE_ID]-test-strategy.md.
> Shall I proceed to implementation planning?"

---

### Phase 3 — Implementation

**Trigger:** Your approval to proceed from Phase 2.

**What the lead agent does:**
1. Spawns a **Plan sub-agent** to design the implementation:

```
You are a software architect. Your job is to produce an implementation plan.
Do NOT write any code yet.

Read these files in full:
  - /media/dan/fdrive/codeprojects/PersonalFinance/Lib/AgentContext.txt
  - [BRD_FILE_PATH]
  - ProjectDocuments/FSDs/[FEATURE_ID]-fsd.md
  - [every test file written in Phase 2]
  - [every source file that will need to change, based on the FSD]

Produce a step-by-step implementation plan:
  1. List every file that must be created or modified, in dependency order.
  2. For each file, describe exactly what changes are needed and why.
  3. Note which tests each change will cause to go from Skip → Pass.
  4. Identify any risk: breaking change to existing tests, DB migration, etc.
  5. Do NOT write C# code in this plan — pseudocode and descriptions only.

Write the plan to: ProjectDocuments/Pipeline/[FEATURE_ID]-impl-plan.md
```

2. Reads the plan.
3. **Immediately** writes any risks, open architectural choices, or items requiring Dan's input
   into an "Open Items" section at the bottom of `[FEATURE_ID]-impl-plan.md` — each with a
   `Dan's decision: ___` placeholder — **before presenting anything to Dan.**
4. Presents the plan summary and all open items to you.
5. Acts as scribe: fills in each placeholder in the impl-plan.md as Dan responds.
6. Only after your approval of the final plan: implements the changes in the main session
   (or spawns targeted sub-agents for large, parallelizable chunks).
7. Runs `dotnet test` after every logical chunk. Reports results.
8. Removes `Skip` attributes from tests as each piece is implemented and passing.

**Loose-ends rule:** Any risk or open architectural choice must be written to impl-plan.md
before being raised in chat. Implementation does not begin until all open items are resolved
and the plan file reflects Dan's decisions.

**Final gate:** Lead agent updates `Pipeline/[FEATURE_ID]-status.md` (Phase 3 → COMPLETE),
then asks:
> "All [N] tests pass. Implementation is complete. Shall I commit?"

---

## The Session Initialization Prompt

Paste this at the start of any Claude Code session where you want to run the pipeline:

```
Read /media/dan/fdrive/codeprojects/PersonalFinance/Lib/AgentContext.txt and
/media/dan/fdrive/codeprojects/PersonalFinance/ProjectDocuments/ClaudePrompt_0_how_do_I_set_this_up.md.

I want to run the SDLC pipeline for the BRD at: [BRD_FILE_PATH]

Begin at Phase 0. Do not proceed to any phase without my explicit approval.
```

That is the entire trigger. Everything else follows from the phase playbook above.

---

## Resuming After a Session Ends

If your session runs out of context, you close your terminal, or you return after a long
break, start a new Claude Code session and paste this:

```
Read /media/dan/fdrive/codeprojects/PersonalFinance/Lib/AgentContext.txt and
/media/dan/fdrive/codeprojects/PersonalFinance/ProjectDocuments/ClaudePrompt_0_how_do_I_set_this_up.md.

I am resuming an in-progress SDLC pipeline. Read the status file at:
  ProjectDocuments/Pipeline/[FEATURE_ID]-status.md

Then read every artifact listed as COMPLETE in that file so you have full context.
Resume from the first phase that is IN PROGRESS or NOT STARTED.
Do not re-run any phase that is already COMPLETE.
Do not proceed past the next phase gate without my explicit approval.
```

The lead agent reads the status file, sees exactly where the pipeline stood, reads the
completed artifacts for context, and picks up without repeating any finished work.

**If a phase was IN PROGRESS when the session ended** (sub-agent was mid-run), the
output file may be incomplete. The lead agent will detect this by checking whether the
artifact exists and is well-formed. If it is not, it will re-run that phase from scratch
and tell you it is doing so.

**If you are unsure what feature IDs exist**, ask the lead agent:
```
List all pipeline status files under ProjectDocuments/Pipeline/ and show me their
current phase status.
```

---

## Anti-Hallucination Rules (enforced in every sub-agent prompt)

Every sub-agent prompt above already contains these constraints, but they are listed
here explicitly so you can verify them when editing prompts:

1. **Cite before claiming.** No agent may describe how the codebase works without
   citing the specific file path and line number it read.
2. **Distinguish known from inferred.** If an agent cannot find direct evidence, it
   must say "I could not find evidence for this; I am inferring from [X]."
3. **Flag gaps explicitly.** Agents must list what they searched for and did not find,
   not silently skip it.
4. **No forward-reaching.** BRD agents do not design. FSD agents do not write tests.
   Test agents do not implement. Each agent's prompt explicitly names what it must NOT do.
5. **Build verification.** Any agent that writes C# must compile before reporting back.

---

## Improving the Process

When a phase produces a bad result, record it here:

**Retro log:**
```
[YYYY-MM-DD] Phase N produced [problem]. Root cause: [which prompt constraint was missing].
Fix applied: [what was added to the prompt template above].
```

This document is the living source of truth for the pipeline. Update the prompt
templates directly when you find a gap — do not maintain a separate "lessons learned"
file that will drift out of sync.