## ADDED Requirements

### Requirement: Sidebar Component

The system SHALL provide a reusable Sidebar component for navigation.

#### Scenario: Sidebar displays navigation links
- **WHEN** the Sidebar component renders
- **THEN** navigation links to Dashboard and Logs are displayed
- **AND** each link uses React Router's Link component for client-side navigation

#### Scenario: Sidebar has active state indication
- **WHEN** the current route matches a navigation link
- **THEN** that link appears visually highlighted or selected

#### Scenario: Sidebar styling is consistent
- **WHEN** the Sidebar renders
- **THEN** it has appropriate background color, text color, and spacing
- **AND** it uses TailwindCSS utility classes

### Requirement: Header Component

The system SHALL provide a reusable Header component for the admin layout.

#### Scenario: Header displays title
- **WHEN** the Header component renders
- **THEN** it displays "SkySim Logger Admin" as the application title

#### Scenario: Header has logout placeholder
- **WHEN** the Header renders
- **THEN** a logout button placeholder exists (no real logout functionality)
- **AND** it is positioned on the right side of the header

### Requirement: PageHeader Component

The system SHALL provide a reusable PageHeader component for consistent page titles.

#### Scenario: PageHeader displays title and subtitle
- **WHEN** the PageHeader renders
- **THEN** it displays the provided title
- **AND** it displays the provided subtitle below the title

#### Scenario: PageHeader supports children
- **WHEN** the PageHeader renders with children
- **THEN** the children are rendered after the subtitle

### Requirement: StatusBadge Component

The system SHALL provide a reusable StatusBadge component for displaying flow and action statuses.

#### Scenario: StatusBadge renders with correct color for SUCCESS
- **WHEN** StatusBadge renders with status="SUCCESS"
- **THEN** it displays green background and text

#### Scenario: StatusBadge renders with correct color for FAILED
- **WHEN** StatusBadge renders with status="FAILED"
- **THEN** it displays red background and text

#### Scenario: StatusBadge renders with correct color for RUNNING
- **WHEN** StatusBadge renders with status="RUNNING"
- **THEN** it displays blue background and text

#### Scenario: StatusBadge renders with correct color for PARTIAL_FAILED
- **WHEN** StatusBadge renders with status="PARTIAL_FAILED"
- **THEN** it displays amber background and text

### Requirement: MetricCard Component

The system SHALL provide a reusable MetricCard component for dashboard metrics.

#### Scenario: MetricCard displays title and value
- **WHEN** MetricCard renders
- **THEN** it displays the provided title
- **AND** it displays the provided numeric or string value

#### Scenario: MetricCard has consistent styling
- **WHEN** MetricCard renders
- **THEN** it has white background, rounded corners, and subtle shadow
- **AND** the value uses larger, bold text

### Requirement: EmptyState Component

The system SHALL provide a reusable EmptyState component for displaying empty data states.

#### Scenario: EmptyState displays message
- **WHEN** EmptyState renders
- **THEN** it displays a message indicating no data is available
- **AND** it may include an optional icon or visual indicator

#### Scenario: EmptyState styling is neutral
- **WHEN** EmptyState renders
- **THEN** it uses gray text and neutral styling
- **AND** it is centered within its container
