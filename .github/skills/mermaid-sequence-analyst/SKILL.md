---
name: mermaid-sequence-analyst
description: "Create Mermaid.js sequence diagrams from business flow or source code analysis. Use when user asks to act as System Analyst/Software Architect, requires strict manual numbering (no autonumber), enforced white background init block, Client->Page->Controller interaction order, activation balance (+/-), and alt/else/opt/loop fragments."
argument-hint: "Paste business flow or code context; include endpoint and error branches if available."
user-invocable: true
---

# Mermaid Sequence Analyst

## Outcome

Produce a single Mermaid sequence diagram code block that models the provided business flow/code as a System Analyst and Software Architect.

## Mandatory Output Contract

1. Return exactly one fenced block using mermaid syntax.
2. Do not add any text before or after the mermaid block.
3. Keep messages short and action-oriented.

## Hard Rules

1. First line must be exactly:
   %%{init: { 'theme': 'default', 'themeVariables': { 'background': '#ffffff', 'activationBkgColor': '#ffffff', 'altBackground': '#ffffff', 'actorBkg': '#ffffff', 'participantBkg': '#ffffff' } } }%%
2. Never use autonumber.
3. Manually number every message as: "N. Message".
4. Interaction order starts from actor Client -> participant Page -> participant Controller -> lower layers.
5. Call message uses solid arrow with activation start: `->>+`.
6. Return message uses dotted arrow with activation end: `-->>-`.
7. Self-processing message uses `->>`.
8. Conditional branches must use `alt` and `else`.
9. Optional-only logic must use `opt`.
10. Loops must use `loop`.
11. Ensure activation starts and ends are balanced in each branch to avoid render errors.
12. ALWAYS show exact class/interface names for Service and Repository participants.
13. Do NOT use abbreviated aliases like `Svc`, `Repo`, `Service1`, `Repository1`.
14. Use explicit participant ids that reflect real names, for example:
    - `participant CategoryService as :ICategoryService`
    - `participant CategoryRepository as :ICategoryRepository`
15. Message lines must use those explicit ids (for example: `CategoryService->>+CategoryRepository`) so reader can see clearly which service/repository is involved.

## Standard Diagram Skeleton

```mermaid
%%{init: { 'theme': 'default', 'themeVariables': { 'background': '#ffffff', 'activationBkgColor': '#ffffff', 'altBackground': '#ffffff', 'actorBkg': '#ffffff', 'participantBkg': '#ffffff' } } }%%
sequenceDiagram
    actor C as Client
    participant Page as :PageName
    participant Controller as :ControllerName
    participant CategoryService as :ICategoryService
    participant CategoryRepository as :ICategoryRepository
```

## Procedure

1. Parse user input and extract:

- Primary use case
- Actors and system boundaries
- Main success path
- Error/exception branches
- Optional steps and loops

2. Normalize participants in this order when applicable:

- `Client`
- `Page`
- `Controller`
- `Auth`/`Service`/`Repository`/`DB`/other downstream components

3. Build main flow first:

- Start numbering at 1.
- First message should begin from `Client` to `Page`.
- Keep each message concise and action-driven.

4. Add branch fragments:

- Use `alt`/`else` for success-vs-failure or if/else outcomes.
- Use `opt` for optional execution.
- Use `loop` for repeated actions.

5. Apply arrow and activation discipline:

- Outbound calls: `Caller->>+Callee: N. Action`
- Returns: `Callee-->>-Caller: N. Result`
- Internal processing: `CategoryService->>CategoryService: N. Map entity`

6. Validate before finalizing:

- White-background init block is first line.
- No `autonumber` token appears.
- Every message is manually numbered in ascending order.
- `+` and `-` activations are balanced across all branches.
- Output is a single mermaid code fence only.

## Quality Checklist

- Correct participant order and naming consistency.
- Accurate API/action wording (short verbs).
- Explicit error path with clear outcome (for example: Throw Exception, Show Error Message).
- No narrative prose outside diagram.

## Ask-Back When Input Is Incomplete

If key details are missing, ask only what is necessary:

1. Main use case and endpoint/action name
2. Success response and error responses
3. Optional and loop conditions
4. Exact downstream components (Auth/Service/Repo/DB)
```
