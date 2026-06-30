## ADDED Requirements

### Requirement: Login with Keycloak password grant

The system SHALL allow users to authenticate using Keycloak's password grant flow by submitting username and password credentials.

#### Scenario: Successful login with valid credentials

- **WHEN** user enters valid username and password and clicks login button
- **THEN** system sends POST request to Keycloak token endpoint with form-urlencoded body containing client_id, username, password, and grant_type
- **AND** system stores the access_token in localStorage under key `skysim_logger_access_token`
- **AND** system navigates user to `/dashboard`
- **AND** login button is disabled during submission

#### Scenario: Failed login with invalid credentials

- **WHEN** user enters invalid username or password and clicks login button
- **THEN** system displays inline error message "Login failed. Please check your username and password."
- **AND** user remains on `/login` page
- **AND** login button is re-enabled after failure

#### Scenario: Failed login due to network error

- **WHEN** user submits login but Keycloak server is unreachable
- **THEN** system displays inline error message "Unable to connect to authentication server."
- **AND** user remains on `/login` page

#### Scenario: Empty credentials validation

- **WHEN** user submits login with empty username or password
- **THEN** system displays inline error message "Username and password are required."
- **AND** no request is sent to Keycloak
- **AND** user remains on `/login` page

### Requirement: Logout functionality

The system SHALL allow authenticated users to log out and clear their session.

#### Scenario: User logs out from header

- **WHEN** user clicks logout button in the header
- **THEN** system removes `skysim_logger_access_token` from localStorage
- **AND** system navigates user to `/login`

### Requirement: Token presence check

The system SHALL determine user authentication status by checking token existence in localStorage.

#### Scenario: User is authenticated

- **WHEN** `isAuthenticated()` is called and `skysim_logger_access_token` exists in localStorage
- **THEN** function returns `true`

#### Scenario: User is not authenticated

- **WHEN** `isAuthenticated()` is called and `skysim_logger_access_token` does not exist in localStorage
- **THEN** function returns `false`

### Requirement: Access token retrieval

The system SHALL provide a function to retrieve the stored access token.

#### Scenario: Get access token when stored

- **WHEN** `getAccessToken()` is called and token exists
- **THEN** function returns the stored token string

#### Scenario: Get access token when not stored

- **WHEN** `getAccessToken()` is called and no token exists
- **THEN** function returns `null`

### Requirement: Protected route access control

The system SHALL protect routes from unauthenticated access by redirecting to login.

#### Scenario: Authenticated user accesses protected route

- **WHEN** authenticated user navigates to `/dashboard`
- **THEN** system renders the dashboard page

#### Scenario: Unauthenticated user accesses protected route

- **WHEN** unauthenticated user navigates directly to `/dashboard`
- **THEN** system redirects user to `/login`

#### Scenario: Unauthenticated user accesses log list

- **WHEN** unauthenticated user navigates to `/logs`
- **THEN** system redirects user to `/login`

#### Scenario: Unauthenticated user accesses log detail

- **WHEN** unauthenticated user navigates to `/logs/abc123`
- **THEN** system redirects user to `/login`

### Requirement: Login page is public

The system SHALL allow unauthenticated access to the login page while redirecting authenticated users.

#### Scenario: Unauthenticated user visits login page

- **WHEN** user navigates to `/login`
- **THEN** system renders the login form
- **AND** no redirect occurs

#### Scenario: Authenticated user visits login page

- **WHEN** authenticated user navigates to `/login`
- **THEN** system redirects user to `/dashboard`

### Requirement: Login page default placeholders

The login form SHALL display user-friendly placeholder text in input fields.

#### Scenario: Login form displays placeholders

- **WHEN** login page renders
- **THEN** username input shows placeholder "Enter your username"
- **AND** password input shows placeholder "Enter your password"
