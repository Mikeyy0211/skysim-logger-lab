---
name: code-review
description: Review code for quality, security, and maintainability in the skysim-logger-lab project. Use when reviewing pull requests, examining code changes, refactoring, or when the user asks for a code review.
disable-model-invocation: true
---

# Code Review and Clean Code Assistant

Use this skill when reviewing, refactoring, or improving code quality in the skysim-logger-lab project.

## Review Checklist

When reviewing code, check:

### Readability & Naming
- [ ] Is the code easy to read?
- [ ] Are names clear and consistent?
- [ ] Are responsibilities separated correctly?

### Backend Architecture
- [ ] Are Controllers thin?
- [ ] Is business logic outside Controllers?
- [ ] Are DTOs separated from Entities?
- [ ] Are async methods awaited correctly?
- [ ] Are exceptions handled clearly?
- [ ] Are logs meaningful but not leaking sensitive data?

### Kafka & Data Integrity
- [ ] Is Kafka offset committed only after DB save succeeds?
- [ ] Is idempotency handled?
- [ ] Are database queries paginated?
- [ ] Are indexes aligned with filters?

### API Design
- [ ] Does the API return consistent responses?
- [ ] Does the frontend use TypeScript correctly?
- [ ] Does the frontend avoid unnecessary `any`?

### Frontend Quality
- [ ] Are components reusable and not too large?
- [ ] Is Redux Toolkit used only where global state is needed?
- [ ] Is Axios separated into API service files?

### Code Duplication
- [ ] Is there duplicated code that should be extracted?

## Providing Feedback

When reviewing:

- Point out concrete issues.
- Explain why each issue matters.
- Suggest a cleaner version.
- Do not rewrite everything if a small refactor is enough.
- Prioritize correctness, maintainability, and code review readiness.

Format feedback as:
- **Critical**: Must fix before merge
- **Suggestion**: Consider improving
- **Nice to have**: Optional enhancement

## Sensitive Fields to Mask

When reviewing logging code, ensure these fields are masked:
- password
- access_token
- refresh_token
- authorization
- otp
- cardNumber
- cvv
- paymentSecret
- secret
- token
