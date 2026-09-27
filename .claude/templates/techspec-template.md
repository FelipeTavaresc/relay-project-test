# Technical Specification (Tech Spec) Template

## Executive Summary

[Provide a short technical overview of the solution approach. Summarize the main architectural decisions and the implementation strategy in 1–2 paragraphs.]

## System Architecture

### Component Overview

[Brief description of the primary components and their responsibilities:

- Component names and primary responsibilities
- Key relationships between components
- High-level data flow overview]

## Implementation Design

### Main Interfaces

[Define the main service interfaces (≤20 lines per example):

```go
// Example interface definition
type ServiceName interface {
    MethodName(ctx context.Context, input InputType) (OutputType, error)
}
```

]

### Data Models

[Define essential data structures:

- Primary domain entities (if applicable)
- Request/response types
- Database schemas (if applicable)]

### API Endpoints

[List API endpoints if applicable:

- Method and path (e.g., `POST /api/v0/resource`)
- Brief description
- Request/response format references]

## Integration Points

[Include only if the feature requires external integrations:

- External services or APIs
- Authentication requirements
- Error handling approach]

## Testing Approach

### Unit Tests

[Describe the unit testing strategy:

- Main components to test
- Mocking requirements (only for external services)
- Critical test scenarios]

### Integration Tests

[If needed, describe integration testing:

- Components to test together
- Test data requirements]

## Development Sequencing

### Build Order

[Define the implementation sequence:

1. First component/feature (why first)
2. Second component/feature (dependencies)
3. Subsequent components
4. Integration and testing]

### Technical Dependencies

[List any blocking dependencies:

- Required infrastructure
- Availability of external services]

## Monitoring and Observability

[Define monitoring approach using existing infrastructure:

- Metrics to expose and their format
- Key logs and log levels
- Integration with whatever logging/monitoring the project already uses]

## Technical Considerations

### Key Decisions

[Document important technical decisions:

- Chosen approach and rationale
- Considered trade-offs
- Alternatives rejected and why]

### Known Risks

[Identify technical risks:

- Potential challenges
- Mitigation approaches
- Areas requiring research]

### Special Requirements

[If applicable:

- Performance requirements (specific metrics)
- Security considerations beyond standard auth
- Additional monitoring needs]

### Standards Compliance

[Research the rules in `.claude/rules/` (and the root `CLAUDE.md`, if present) that apply to this techspec and list them below:]

### Relevant Files

[List relevant files here]
