## Context

The LoggerMiddleware has been reviewed and accepted by PM. PM has provided a dedicated Kafka environment for testing:
- BootstrapServers: `171.244.49.17:9092`
- Topic: `system-event-log`

Currently, `KafkaLogProducer` hard-codes the topic as `skysim.action.logs`, making it impossible to switch environments without code changes. This prevents seamless testing against PM's Kafka environment.

**Stakeholders**: PM team, backend developers integrating Logger middleware

## Goals / Non-Goals

**Goals:**
- Make Kafka topic configurable for the Logger.Client producer
- Provide easy registration extension methods for consuming services
- Add PM environment configuration for testing
- Document how to switch between local and PM Kafka

**Non-Goals:**
- Redesigning LoggerMiddleware logic
- Adding business action logging
- Modifying frontend, database, or API contracts
- Hard-coding PM Kafka IP in C# code
- Committing secrets to repository

## Decisions

### Decision 1: Extract Kafka topic to configuration

**Choice**: Add `Kafka:Producer:Topic` configuration option to `KafkaLogProducer`.

**Rationale**:
- Minimal change to existing code
- Follows existing pattern where `BootstrapServers` is already configurable
- Aligns with .NET Options pattern (`IOptions<T>`)

**Alternative considered**: Create separate environment profiles for each Kafka setup.
- **Rejected**: Too complex; requires environment-specific code paths.

### Decision 2: Use extension methods for registration

**Choice**: Create `AddSkysimLogger()` and `UseSkysimLogger()` extension methods.

**Rationale**:
- Reduces boilerplate in consuming services
- Makes it clear what services/middleware are required
- Follows common ASP.NET Core patterns (e.g., `AddAuthentication()`, `UseAuthentication()`)

**Alternative considered**: Keep current manual registration pattern.
- **Rejected**: More error-prone and harder for PM to integrate quickly.

### Decision 3: Separate PM configuration file

**Choice**: Create `appsettings.PM.json` for PM Kafka environment.

**Rationale**:
- Isolates PM-specific settings from local development
- Easy to swap via `--environment PM` or `ASPNETCORE_ENVIRONMENT=PM`
- Can be excluded from git or stored in password manager

**Alternative considered**: Environment variables only.
- **Rejected**: Environment variables work but config file is more discoverable for testing.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Topic name mismatch between producer and consumer | Document that PM must use `system-event-log` for producer; Logger.Api consumer uses separate configurable topic |
| Configuration not picked up correctly | Add validation/logging on startup to confirm topic is set |
| PM forgets to set environment | Document clear steps in README/notes |

## Migration Plan

1. **Before PM integration** (Today):
   - Implement config-driven topic in `KafkaLogProducer`
   - Add extension methods
   - Create `appsettings.PM.json`
   - Verify SampleService runs against local Kafka

2. **PM integration day**:
   - PM copies `appsettings.PM.json` as template
   - PM runs service with `ASPNETCORE_ENVIRONMENT=PM`
   - Verify logs appear in PM's `system-event-log` topic

3. **Rollback**:
   - Revert `KafkaLogProducer` to hard-coded topic (if needed)
   - Use default `appsettings.json` with local Kafka

## Open Questions

1. **Should `Kafka:Producer:Topic` be required or have a default?**
   - Decision: Default to `skysim.action.logs` for backward compatibility

2. **Should Logger.Api consumer also support PM Kafka?**
   - Decision: Optional - Logger.Api consumer can optionally switch via config if PM wants end-to-end testing
