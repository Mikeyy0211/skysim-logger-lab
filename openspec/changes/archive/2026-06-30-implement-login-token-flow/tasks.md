## 1. Token Storage Utility

- [x] 1.1 Create `frontend/src/utils/tokenStorage.ts`
- [x] 1.2 Implement `getToken()` function to retrieve token from localStorage
- [x] 1.3 Implement `setToken(token: string)` function to store token
- [x] 1.4 Implement `removeToken()` function to clear token
- [x] 1.5 Define localStorage key constant `skysim_logger_access_token`

## 2. Auth Service

- [x] 2.1 Create `frontend/src/services/authService.ts`
- [x] 2.2 Implement `login(username: string, password: string)` function
- [x] 2.3 Use Keycloak token endpoint built from VITE_KEYCLOAK_BASE_URL, VITE_KEYCLOAK_REALM, and VITE_KEYCLOAK_CLIENT_ID with form-urlencoded body (do not hardcode values)
- [x] 2.4 Store access_token in localStorage on successful login
- [x] 2.5 Implement `logout()` function to remove token
- [x] 2.6 Implement `getAccessToken()` function to retrieve token
- [x] 2.7 Implement `isAuthenticated()` function to check token existence
- [x] 2.8 Handle authentication errors with user-friendly messages

## 3. Protected Route Component

- [x] 3.1 Create `frontend/src/components/ProtectedRoute.tsx`
- [x] 3.2 Check authentication status using `isAuthenticated()`
- [x] 3.3 Redirect to `/login` if not authenticated
- [x] 3.4 Render child routes if authenticated

## 4. Axios Interceptor

- [x] 4.1 Update `frontend/src/services/api.ts`
- [x] 4.2 Add request interceptor to attach Bearer token
- [x] 4.3 Check for token existence before attaching header
- [x] 4.4 Format header as `Authorization: Bearer <token>`

## 5. Login Page Updates

- [x] 5.1 Update `frontend/src/pages/LoginPage.tsx`
- [x] 5.2 Add controlled inputs for username and password
- [x] 5.3 Add user-friendly placeholders ("Enter your username", "Enter your password")
- [x] 5.4 Connect form submit to `authService.login()`
- [x] 5.5 Navigate to `/dashboard` on successful login
- [x] 5.6 Display inline error message on login failure
- [x] 5.7 Disable login button while submitting
- [x] 5.8 Validate username and password before calling authService.login()
- [x] 5.9 Redirect authenticated users from /login to /dashboard

## 6. Header Logout Updates

- [x] 6.1 Update `frontend/src/components/Header.tsx`
- [x] 6.2 Connect logout button to `authService.logout()`
- [x] 6.3 Navigate to `/login` after logout

## 7. Router Configuration

- [x] 7.1 Update `frontend/src/app/Router.tsx`
- [x] 7.2 Wrap protected routes with `ProtectedRoute` component
- [x] 7.3 Apply protection to `/dashboard` route
- [x] 7.4 Apply protection to `/logs` route
- [x] 7.5 Apply protection to `/logs/:flowId` route
- [x] 7.6 Keep `/login` route unprotected

## 8. Verification

- [x] 8.1 Run `npm run build` to verify no TypeScript errors
- [x] 8.2 Verify login page renders correctly
- [x] 8.3 Verify protected routes redirect to login when unauthenticated
- [x] 8.4 Verify logout clears token and redirects to login
- [x] 8.5 Verify Axios interceptor attaches Bearer token when authenticated
- [x] 8.6 Verify login with logger_admin / admin123 redirects to /dashboard when Keycloak is running
