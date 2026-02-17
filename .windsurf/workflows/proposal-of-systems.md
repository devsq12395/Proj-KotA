---
auto_execution_mode: 3
---

---
description: Create a System Proposal and Log
---

Use this workflow whenever you (or I) are asked to create a new “system” (integration, subsystem, service, tooling pipeline, etc.) in this repo.

## Rules (must follow)

1) Create a proposal first
- File location: `Docs/Dev/AI-Proposals/`
- File name: `Docs/Dev/AI-Proposals/<SYSTEM_NAME>_PROPOSAL.md`
- The proposal must be approved by a developer before implementation begins.

2) Start a dev log when implementation begins
- File location: `Docs/Dev/System-Dev-Logs/`
- File name: `Docs/Dev/System-Dev-Logs/<SYSTEM_NAME>.md`
- Keep the log updated throughout implementation.

3) When implementation is finished (documentation required)
- If the system added a new feature, create a new markdown file in `Docs/Dev/Features/`.
- If the system changed existing features, update the affected markdown files in `Docs/Dev/Features/`.

Where `<SYSTEM_NAME>` is `UPPER_SNAKE_CASE` (example: `STEAM_INTEGRATION`).

## System proposal (.md) guideline

The system proposal should be a single markdown file that answers:
- What are we building and why?
- Where does it connect to existing code/data?
- What are the implementation steps and milestones?
- What are the risks, security concerns, and testing strategy?

### System proposal template

Copy/paste and fill in:

```md
# <System Name> Proposal

## Summary

## Goals

## Non-Goals

## Scope
- In scope:
- Out of scope:

## References / Existing Files
- Docs:
- Code:

## Architecture / Design
- Components:
- Data flow:
- API/Interfaces:

## Data / Settings / Configuration
- New settings:
- Where stored:
- Defaults:

## Step-by-Step Integration Plan
1)
2)
3)

## Milestones
- Milestone 1:
- Milestone 2:
- Milestone 3:

## Security Considerations

## Risks and Mitigations

## Testing Plan

## Rollout Plan

## Deliverables
- Files to be added/modified:
```

## System dev log (.md) guideline

The system dev log should be a running, chronological record of work, including decisions and verification steps.

### System dev log template

Copy/paste and fill in:

```md
# <System Name>

## Status
- Current: Not Started | In Progress | Blocked | Completed
- Owner:
- Last Updated:

## Links
- Proposal: `Docs/Dev/AI-Proposals/<SYSTEM_NAME>_PROPOSAL.md`
- Related files:

## Milestones
- [ ] Milestone 1:
- [ ] Milestone 2:
- [ ] Milestone 3:

## Progress Log

### YYYY-MM-DD
- What changed:
- Files touched:
- Notes:

## Decisions

### YYYY-MM-DD — <Decision Title>
- Decision:
- Alternatives considered:
- Rationale:

## Blockers
- <blocker>

## Verification
- How to test:
- Expected result:
```
