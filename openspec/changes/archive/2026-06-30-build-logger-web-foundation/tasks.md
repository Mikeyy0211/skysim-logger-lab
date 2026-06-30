## 1. Project Initialization

- [x] 1.1 Create `frontend/` directory
- [x] 1.2 Initialize Vite + React + TypeScript project inside `frontend/` using `npm create vite@latest . -- --template react-ts`
- [x] 1.3 Update `package.json` with project name `skysim-logger-web`
- [x] 1.4 Install dependencies: `react-router-dom`, `axios`, `tailwindcss@3`, `postcss`, `autoprefixer`
- [x] 1.5 Initialize TailwindCSS with `npx tailwindcss init -p`

## 2. Configuration Files

- [x] 2.1 Configure `vite.config.ts` with path aliases if needed
- [x] 2.2 Configure `tailwind.config.js` for React content paths
- [x] 2.3 Configure `tsconfig.json` with proper paths
- [x] 2.4 Create `.env.example` with environment variables
- [x] 2.5 Ensure `.env` and `.env.local` are ignored by git (create/update `.gitignore`)
- [x] 2.6 Document in README that developers should copy `.env.example` to `.env`

## 3. Folder Structure

- [x] 3.1 Create `src/app/` directory
- [x] 3.2 Create `src/components/` directory
- [x] 3.3 Create `src/layouts/` directory
- [x] 3.4 Create `src/pages/` directory
- [x] 3.5 Create `src/services/` directory
- [x] 3.6 Create `src/types/` directory
- [x] 3.7 Create `src/hooks/` directory
- [x] 3.8 Create `src/utils/` directory

## 4. App Entry Point

- [x] 4.1 Update `src/main.tsx` with React Router
- [x] 4.2 Create `src/app/Router.tsx` with route configuration
- [x] 4.3 Update `src/index.css` with TailwindCSS directives

## 5. Layout Components

- [x] 5.1 Create `src/layouts/AdminLayout.tsx` with sidebar placeholder
- [x] 5.2 Style AdminLayout using TailwindCSS utility classes only
- [x] 5.3 Add sidebar links for Dashboard and Logs navigation
- [x] 5.4 Add logout button placeholder in header

## 6. Page Components

- [x] 6.1 Create `src/pages/LoginPage.tsx` with page title
- [x] 6.2 Create `src/pages/DashboardPage.tsx` with page title
- [x] 6.3 Create `src/pages/LogListPage.tsx` with page title
- [x] 6.4 Create `src/pages/LogDetailPage.tsx` with page title and flowId param

## 7. API Services

- [x] 7.1 Create `src/services/api.ts` with Axios instance
- [x] 7.2 Export configured Axios instance

## 8. Documentation

- [x] 8.1 Create `frontend/README.md` with local run instructions
- [x] 8.2 Document npm install and npm run dev commands
- [x] 8.3 Document environment variable setup

## 9. Verification

- [x] 9.1 Run `npm install` successfully
- [x] 9.2 Run `npm run dev` and verify server starts
- [x] 9.3 Verify `/login` renders correctly
- [x] 9.4 Verify `/dashboard` renders correctly
- [x] 9.5 Verify `/logs` renders correctly
- [x] 9.6 Verify `/logs/:flowId` renders correctly
- [x] 9.7 Run `npm run build` and verify success
