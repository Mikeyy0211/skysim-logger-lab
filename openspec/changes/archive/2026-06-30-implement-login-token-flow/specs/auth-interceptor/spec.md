## ADDED Requirements

### Requirement: Axios Bearer token attachment

The system SHALL automatically attach the stored access token to all API requests made through the configured axios instance.

#### Scenario: Request includes Bearer token when authenticated

- **WHEN** axios request is made and `skysim_logger_access_token` exists in localStorage
- **THEN** request header includes `Authorization: Bearer <token>`

#### Scenario: Request has no Authorization header when not authenticated

- **WHEN** axios request is made and no token exists in localStorage
- **THEN** request does not include `Authorization` header
