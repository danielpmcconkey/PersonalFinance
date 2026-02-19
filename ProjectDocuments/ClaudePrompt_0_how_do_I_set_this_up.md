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
  Lib.Tests/
    TestStrategy/
      BusinessOutcomes.md    ← test planning sub-agent updates this
      GapAnalysis.md         ← generated after test planning
```

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
4. Presents every open question to you in the main chat.
5. Acts as scribe: updates the BRD's "Open Questions" section with your answers.
6. Repeats until Unanswered Questions list is empty.

**Phase gate:** Lead agent asks:
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
3. Presents a summary and all OPEN DECISION items to you.
4. Acts as scribe: updates the FSD with your decisions.
5. Repeats until no OPEN DECISION items remain.

**Phase gate:** Lead agent asks:
> "FSD is complete with no open decisions. Summary: [N] functional specs covering
> BRD-001 through BRD-NNN. Shall I proceed to test planning?"

---

### Phase 2 — Test Planning and Test Writing

**Trigger:** Your approval to proceed from Phase 1.

**What the lead agent does:**
1. Spawns a **general-purpose sub-agent** to update BusinessOutcomes.md:

```
You are a test planner for a C# .NET 8.0 financial simulation. Your job has
two parts: (1) update BusinessOutcomes.md with new outcome rows, and (2) write
the xUnit test stubs. Do NOT write implementation code.

Read these files in full before writing anything:
  - /media/dan/fdrive/codeprojects/PersonalFinance/Lib/AgentContext.txt
  - [BRD_FILE_PATH]
  - ProjectDocuments/FSDs/[FEATURE_ID]-fsd.md
  - Lib.Tests/TestStrategy/BusinessOutcomes.md
  - Lib.Tests/TestDataManager.cs  (to know what test helpers exist)
  - [2-3 existing test files most similar to what this feature needs]

For each FSD-NNN item:
  - Identify every distinct observable business outcome that a test should assert.
  - Add a row to the appropriate section of BusinessOutcomes.md with status C.
  - Include the FSD-NNN and BRD-NNN trace in the Notes column.

Then write the test file(s):
  - One test class per logical group (follow existing naming conventions).
  - Use [Fact(DisplayName = "§X.Y — description")] for traceability.
  - Write the full test body — not stubs — with realistic assertions.
  - Follow the immutable-result patterns: access results via
    result.newAccounts.InvestmentAccounts.First(...), not named properties,
    after any function that replaces the InvestmentAccounts list.
  - If a test depends on behavior not yet implemented, mark it
    [Fact(Skip = "Not yet implemented — FSD-NNN")] with an explanation.

After writing tests, update each BusinessOutcomes.md row you added from C → A,
citing the test class and method name.

Verify the build compiles before finishing: run
  dotnet build Lib.Tests/Lib.Tests.csproj --no-restore -v q
and fix any errors before reporting back.
```

2. Reviews the new test file and BusinessOutcomes.md changes.
3. Presents coverage summary to you: how many FSD items have tests, any gaps.

**Phase gate:** Lead agent asks:
> "[N] test cases written covering all FSD items. Build passes. [M] tests are
> skipped pending implementation. Shall I proceed to implementation planning?"

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

2. Presents the plan to you and asks: "Does this match your intent? Shall I implement?"
3. Only after your approval: implements the changes in the main session
   (or spawns targeted sub-agents for large, parallelizable chunks).
4. Runs `dotnet test` after every logical chunk. Reports results.
5. Removes `Skip` attributes from tests as each piece is implemented and passing.

**Final gate:** Lead agent asks:
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