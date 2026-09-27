---
description: Pick up the next pending task, summarize it, then implement it
argument-hint: [feature-name slug] [task number, e.g. 2.0] — both optional
---

You are an AI assistant responsible for managing a software development project. Your task is to identify the next available task, perform the necessary setup, and prepare to start work.

## Provided Information

Arguments from the user (may be empty): $ARGUMENTS
- If a feature slug is given, work inside `./tasks/prd-[feature-name]/`; otherwise list the folders under `./tasks/` and ask which one.
- If a task number is given, execute that task; otherwise pick the first task in `tasks.md` whose file has `status: pending` and whose dependencies are `completed`.

## File Locations

- PRD: `./tasks/prd-[feature-name]/prd.md`
- Tech Spec: `./tasks/prd-[feature-name]/techspec.md`
- Tasks: `./tasks/prd-[feature-name]/tasks.md`
- Task file: `./tasks/prd-[feature-name]/[num]_task.md`
- Project rules: `.claude/rules/` and the root `CLAUDE.md` (if present)

## Steps to Execute

### 1. Pre-task Setup
- Read the task definition
- Review PRD context
- Check technical spec requirements
- Understand dependencies from previous tasks
- Set the task file's frontmatter to `status: in-progress`

### 2. Task Analysis
Analyze considering:
- Main objectives of the task
- How the task fits in the project context
- Alignment with project rules and standards
- Possible solutions or approaches

### 3. Task Summary

```
Task ID: [ID or number]
Task Name: [Short name or description]
PRD Context: [Key PRD points]
Tech Spec Requirements: [Primary technical requirements]
Dependencies: [List of dependencies]
Main Objectives: [Primary goals]
Risks/Challenges: [Identified risks or challenges]
```

### 4. Approach Plan

```
1. [First step]
2. [Second step]
3. [Additional steps as needed]
```

## Important Notes

- Always verify against the PRD, the tech spec and the task file
- Implement appropriate solutions without hacks
- Follow all established project standards
- Never invent requirements that are not in the PRD, tech spec or task file — if something is genuinely missing, state the assumption you are proceeding on

## Implementation

After providing the summary and approach, start implementing the task immediately:
- Run required commands
- Make code changes
- Follow project standards
- Ensure all requirements are met

YOU MUST begin implementation after the above process.

## Closing Out

When the task is done:
- Run the project's build and tests; report real output, including failures
- Tick the subtask and main-task checkboxes in the task file and in `tasks.md`
- Set the task file's frontmatter to `status: completed`
- Fill in the task file's **Relevant Files** section with the files actually touched
- Report what is done, what is left, and anything you deliberately skipped

## Committing changes

After completing the task, stage the files you created or modified and commit them.

- Stage only the files related to this task. Don't use `git add -A` or `git add .` if there are unrelated changes in the working tree.
- Use the Conventional Commits format: `<type>: <short description>`
- Common types: `feat` (new feature), `fix` (bug fix), `refactor` (code change with no behavior change), `docs`, `test`, `chore`.
- Keep the message short and simple: a single line, lowercase, imperative mood, no period at the end, ideally under 72 characters.
- Do not add a `Co-Authored-By` trailer or any other attribution to the commit message.
- Do not push unless explicitly asked.

Examples:
feat: implement the foundation
fix: adjust the business logic
refactor: improve code readability

<critical>
Use Context7 (or WebSearch/WebFetch if unavailable) to analyze language, framework and library documentation relevant to the implementation
</critical>
