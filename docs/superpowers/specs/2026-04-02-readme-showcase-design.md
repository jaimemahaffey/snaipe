# Spec: README Overhaul & Showcase

Overhaul the Snaipe `README.md` to transform it from a planning document into a high-impact visual showcase of the project's current capabilities.

## Goals

- **Visual Showcase:** Transition the README to lead with visuals and clear, tool-focused sections.
- **Audience:** Target developers looking for a visual tree inspector for Uno Platform Desktop.
- **Structure:** Organize by distinct "tools" or capabilities (Tree, DataContext, Properties, Pick Mode).
- **Impact:** Use screenshots to demonstrate value immediately.

## Design Sections

### 1. Header & Hero
- **Title:** Snaipe
- **Subtitle:** The visual tree inspector for Uno Platform Desktop.
- **Hero Image:** A side-by-side screenshot of the Snaipe Inspector and the Snaipe Sample App.
- **Value Prop:** A concise 2-3 sentence description of what Snaipe solves (debugging trees, properties, and bindings in real-time).

### 2. Tool-by-Tool Showcase
Each section will feature a title, a brief description, and a dedicated screenshot.

- **Visual Tree Explorer:** Focus on hierarchy navigation and element search.
- **DataContext Drilldown:** Focus on revealing the live ViewModel data behind UI elements. Include a "Pro Tip" callout about debugging bindings.
- **Live Property Editor:** Focus on real-time dependency property editing.
- **Pick Mode:** Focus on the "point-and-click" selection from the app to the inspector.

### 3. Architecture & Tech Stack
- **Architecture:** A simplified text-based or Mermaid diagram showing:
  `[Target App + Snaipe.Agent] <--- Protocol ---> [Snaipe.Inspector]`
- **Tech Stack:** Brief list: .NET 9, Uno Platform 6.x (Skia Desktop), System.IO.Pipelines.

### 4. Quick Start
- Concise instructions for:
  1. Installing the Snaipe.Agent NuGet package.
  2. Adding the one-liner code to start the Snaipe agent in a consuming Uno app.
  3. Running the Inspector.

## Implementation Details

- **Screenshots:** The user will provide the screenshots (placeholder paths will be used in the initial commit).
- **Styling:** Use clean Markdown with blockquotes or emojis to differentiate tool sections.
- **Maintenance:** Ensure the "Status" and "License" sections remain but are moved to the bottom.

## Verification

- **Visual Check:** Render the README in a Markdown previewer to ensure layout and spacing are correct.
- **Link Check:** Ensure all relative links and internal anchors work.
