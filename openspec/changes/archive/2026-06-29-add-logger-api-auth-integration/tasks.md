# Implementation Tasks: Add JWT Bearer Authentication to Logger.Api

## 1. Project Setup

- [x] 1.1 Add `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package to `Skysim.Logger.Api.csproj`

## 2. Configuration

- [x] 2.1 Create `JwtOptions.cs` class in `Options/` folder with Authority, Audience, RequireHttpsMetadata properties
- [x] 2.2 Add JWT configuration section to `appsettings.json` under "Jwt" key
- [x] 2.3 Add JWT configuration to `appsettings.Development.json` with development-appropriate values

## 3. Program.cs Changes

- [x] 3.1 Register JwtOptions configuration using `builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));`
- [x] 3.2 Add authentication services: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)`
- [x] 3.3 Add JWT Bearer configuration with options from JwtOptions
- [x] 3.4 Add authorization services: `AddAuthorization()`
- [x] 3.5 Add `UseAuthentication()` middleware after `UseSwaggerUI()` and health endpoint
- [x] 3.6 Add `UseAuthorization()` middleware after `UseAuthentication()`

## 4. Controller Authorization

- [x] 4.1 Add `[Authorize]` attribute to `LogFlowsController` class (protects all 3 actions)
- [x] 4.2 Add `[Authorize]` attribute to `LogActionsController` class (protects the 1 action)
- [x] 4.3 Verify `/health` endpoint remains anonymous (existing implementation unchanged)

## 5. Swagger Configuration

- [x] 5.1 Add JWT Bearer security definition to SwaggerGen options
- [x] 5.2 Add JWT Bearer security requirement to SwaggerGen options
- [x] 5.3 Add using statement for `Microsoft.AspNetCore.Authentication.JwtBearer` if needed

## 6. Verification

- [x] 6.1 Verify build succeeds: `dotnet build backend/Skysim.Logger.sln`
- [x] 6.2 Verify existing unit tests pass: `dotnet test backend/Skysim.Logger.sln`
- [x] 6.3 Test health endpoint without auth returns 200
- [x] 6.4 Test protected endpoint (LogFlowsController or LogActionsController) without auth returns 401

## 7. OpenSpec Validation

- [x] 7.1 Run `openspec validate add-logger-api-auth-integration --strict`
- [x] 7.2 Fix any validation errors if found

## Testing Notes

For this phase:
- Verify protected endpoints without Authorization header return 401
- Verify Swagger UI shows Authorize button for Bearer token
- Verify existing unit tests continue passing
- Verify build passes

Testing a 200 OK with a valid JWT requires a valid token from an external auth server such as Keycloak (when available in a future phase).
