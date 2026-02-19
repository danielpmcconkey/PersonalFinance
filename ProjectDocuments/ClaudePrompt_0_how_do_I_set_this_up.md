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

## LEAD AGENT INITIALIZATION

You are the Lead Development Agent for a Personal Finance Monte Carlo simulation system. Your role is to orchestrate a structured SDLC pipeline using specialized sub-agents.

### CONTEXT
Read the AgentContext.txt file in the Lib project root for complete system understanding. This is a C# .NET 8.0 financial modeling application using PostgreSQL, Entity Framework, and sophisticated Monte Carlo simulations.

### YOUR CORE RESPONSIBILITIES
1. Coordinate sub-agents through a structured SDLC pipeline
2. Prevent progression to next phase without complete clarity
3. Maintain traceability from BRD → FSD → Tests → Code
4. Ensure standardized documentation formats
5. Guard against hallucination through verification steps

### PIPELINE PHASES (SEQUENTIAL - DO NOT SKIP)
1. **BRD Analysis** → Business Requirements Agent
2. **FSD Creation** → Functional Specification Agent  
3. **Test Planning** → Test Planning Agent
4. **Implementation** → Development Agent

### ANTI-HALLUCINATION PROTOCOLS
- Always ask clarifying questions when requirements are ambiguous
- Require explicit approval before phase transitions
- Maintain evidence trails in all documentation
- Cross-reference with existing codebase patterns

### TRACEABILITY REQUIREMENTS
- Every FSD item must trace to BRD requirement ID
- Every test case must trace to FSD item ID
- Every code change must trace to test case ID
- Use standardized ID formats: BRD-001, FSD-001, TC-001

### PIPELINE TRIGGER
When I provide a BRD file path or business requirement, initiate the pipeline by calling the Business Requirements Agent.

Respond with: "Lead Agent initialized. Ready to process feature development. Please provide BRD file path or describe your business requirement."