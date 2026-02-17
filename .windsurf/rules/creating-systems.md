---
trigger: always_on
---

Before creating a new system, you will have to create a .md file proposal on Docs/Dev/AI-Proposals/ folder first. This proposal should contain a step-by-step process of the integration of this new system. The proposal will have to be approved by the dev first before you can proceed to implement.

Once the implementation has started, you will have to log your progress in Docs/Dev/System-Dev-Logs in a .md file that is named after the system you are implementing.

Example:
- System: Steam Integration
- File: `STEAM_INTEGRATION.md`
Your log should look like this:

## Required process

1) Proposal first
- Create a proposal markdown file in `Docs/Dev/AI-Proposals/`.
- The proposal must be approved by a developer before implementation begins.

2) Implementation + progress log
- Once implementation begins, create a dev log markdown file in `Docs/Dev/System-Dev-Logs/`.
- Update the dev log as work progresses.

3) Feature documentation updates (required when implementation is finished)
- If the system adds a new feature, create a new markdown file in `Docs/Dev/Features/`.
- If the system changes existing features, update the affected feature markdown files in `Docs/Dev/Features/`.

## Naming conventions

- Proposal file: `Docs/Dev/AI-Proposals/<SYSTEM_NAME>_PROPOSAL.md`
- Dev log file: `Docs/Dev/System-Dev-Logs/<SYSTEM_NAME>.md`

Where `<SYSTEM_NAME>` is `UPPER_SNAKE_CASE` (example: `STEAM_INTEGRATION`).

## Proposal template (required)

The proposal file must follow this structure:

```md
# <System Name> Proposal

## Summary

## Goals

## Non-Goals

## References / Existing Files

## Architecture / Design

## Step-by-Step Integration Plan

## Data / Settings / Configuration

## Security Considerations

## Risks and Mitigations

## Testing Plan

## Rollout Plan

## Deliverables
```

## System dev log template (required)

Your dev log file in `Docs/Dev/System-Dev-Logs/` must follow this structure:

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
