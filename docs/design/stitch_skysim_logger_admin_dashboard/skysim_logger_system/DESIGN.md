---
name: Skysim Logger System
colors:
  surface: '#faf8ff'
  surface-dim: '#d9d9e5'
  surface-bright: '#faf8ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f3fe'
  surface-container: '#ededf9'
  surface-container-high: '#e7e7f3'
  surface-container-highest: '#e1e2ed'
  on-surface: '#191b23'
  on-surface-variant: '#434655'
  inverse-surface: '#2e3039'
  inverse-on-surface: '#f0f0fb'
  outline: '#737686'
  outline-variant: '#c3c6d7'
  surface-tint: '#0053db'
  primary: '#004ac6'
  on-primary: '#ffffff'
  primary-container: '#2563eb'
  on-primary-container: '#eeefff'
  inverse-primary: '#b4c5ff'
  secondary: '#505f76'
  on-secondary: '#ffffff'
  secondary-container: '#d0e1fb'
  on-secondary-container: '#54647a'
  tertiary: '#943700'
  on-tertiary: '#ffffff'
  tertiary-container: '#bc4800'
  on-tertiary-container: '#ffede6'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dbe1ff'
  primary-fixed-dim: '#b4c5ff'
  on-primary-fixed: '#00174b'
  on-primary-fixed-variant: '#003ea8'
  secondary-fixed: '#d3e4fe'
  secondary-fixed-dim: '#b7c8e1'
  on-secondary-fixed: '#0b1c30'
  on-secondary-fixed-variant: '#38485d'
  tertiary-fixed: '#ffdbcd'
  tertiary-fixed-dim: '#ffb596'
  on-tertiary-fixed: '#360f00'
  on-tertiary-fixed-variant: '#7d2d00'
  background: '#faf8ff'
  on-background: '#191b23'
  surface-variant: '#e1e2ed'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
  code-md:
    fontFamily: JetBrains Mono
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 20px
  label-bold:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 4px
  container-padding-desktop: 32px
  container-padding-mobile: 16px
  gutter: 24px
  stack-sm: 8px
  stack-md: 16px
  stack-lg: 24px
---

## Brand & Style
The design system focuses on a high-utility, enterprise-grade aesthetic that prioritizes clarity, speed of recognition, and reduced cognitive load for DevOps and System Administrators. The brand personality is clinical, reliable, and precise.

Drawing from **Modern Minimalism** and **Corporate** design movements, the system utilizes a vast "white-space-first" philosophy to manage data density. It avoids unnecessary decoration, using subtle borders and intentional hierarchy to organize complex log streams and system metrics into digestible information modules.

## Colors
The color palette is anchored by a high-calibration Professional Blue for primary actions and brand presence. The interface relies heavily on a "Slate" neutral scale to provide soft contrast against the primary background, preventing eye strain during long monitoring sessions.

Status colors are functionally mapped to industry standards but refined for high legibility against white and light gray surfaces. Each status color should be used sparingly for indicators, badges, and critical alerts to ensure they "pop" against the otherwise monochromatic dashboard structure.

## Typography
The system uses **Inter** for all UI elements to ensure maximum readability and a neutral, professional tone. A secondary monospaced font, **JetBrains Mono**, is introduced specifically for log data, timestamps, and metadata to ensure character alignment and technical clarity.

Scale is used to create a clear information hierarchy:
- Large displays for system-wide health summaries.
- Tight, capitalized labels for table headers and metadata categories.
- Optimized line heights for the `body-md` level to ensure long lists of logs remain legible.

## Layout & Spacing
This design system utilizes a **Fixed-Fluid Hybrid Grid**. The sidebar navigation is fixed at 280px, while the main content area expands dynamically. Content is organized into a 12-column grid system with generous 24px gutters to prevent data-heavy tables from feeling cramped.

**Desktop:** 12-column grid, 32px outer margins.
**Tablet:** 8-column grid, 24px outer margins.
**Mobile:** 4-column grid, 16px outer margins (Sidebar collapses into a hamburger menu).

A strict 4px spacing scale (4, 8, 12, 16, 24, 32, 48, 64) is used for all internal component padding and margins to maintain rhythmic consistency.

## Elevation & Depth
Depth is conveyed through **Tonal Layering** and **Low-contrast Outlines** rather than heavy shadows. This maintains a "flat-plus" aesthetic suitable for professional tools.

- **Level 0 (Background):** Slate-50 (#f8fafc) - The base of the application.
- **Level 1 (Cards/Surface):** White (#ffffff) - Used for primary content containers. Features a 1px solid border in Slate-200.
- **Level 2 (Overlays):** White with a soft ambient shadow (Y: 4px, B: 12px, Color: rgba(15, 23, 42, 0.08)) - Used for dropdowns, tooltips, and filter menus.
- **Level 3 (Modals):** White with a deep ambient shadow and a 20% backdrop blur on the layer below.

## Shapes
Following the requirement for a modern, approachable enterprise tool, the system utilizes an exaggerated **rounded-2xl** (1rem / 16px) corner radius for primary containers and cards. 

Interactive elements like buttons and input fields utilize a standard `rounded-lg` (0.5rem / 8px) to maintain a professional balance, while status badges use a full pill shape for instant visual differentiation from structural boxes.

## Components

### Buttons
- **Primary:** Solid Blue-600, White text, 8px radius.
- **Secondary:** White background, Slate-200 border, Slate-700 text.
- **Ghost:** Transparent background, Slate-600 text, used for less frequent actions like "Export".

### Status Badges
Badges use a "Tinted" style: a 10% opacity background of the status color with 100% opacity text of the same color. 
- *Example (Success):* Background: #10b9811a, Text: #10b981.

### Data Tables
Tables are the core of the system. Rows have a fixed height of 48px, 1px bottom borders (Slate-100), and a subtle hover state (Slate-50). The first column (usually Timestamp) uses `code-md` typography.

### Input Fields & Filters
Inputs feature a 1px Slate-300 border that transitions to a 2px Blue-600 ring on focus. Labels are positioned above the field using `label-bold`.

### Cards
Cards are the primary container for dashboard widgets. They must include a 16px padding and use the `rounded-2xl` variable. Header sections within cards should be separated by a 1px horizontal rule.