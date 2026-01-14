# Novus Language Website Design

**Date:** 2026-01-14
**Status:** Approved
**Domain:** novuslang.com

## Overview

The official website for the Novus programming language — a modern systems programming language for the Amiga 68k family of computers.

**Primary Goal:** Serve as a resource for Amiga developers who want a modern alternative to C or assembly.

**Secondary Goals:**
- Attract new users and get them excited about Novus
- Provide comprehensive documentation for existing users
- Build community over time

## Target Audience

A mix of:
- **Active Amiga developers** — Writing code for real or emulated Amigas today
- **Retro-curious programmers** — Modern developers who want to explore Amiga development
- **Amiga nostalgists** — People who remember the Amiga and might be drawn back by a friendlier toolchain

## Design Direction

### Tone
**Retro-modern fusion** — Modern, professional design with subtle Amiga nods. Not full nostalgia, but enough personality to honor the platform.

### Color Palette

Inspired by the Boing Ball demo:

- **Novus Red** — `#E63030` (or similar) — Primary accent, CTAs, highlights
- **Pure White** — `#FFFFFF` — Clean backgrounds, contrast
- **Near Black** — `#1A1A1A` — Text, dark mode backgrounds
- **Warm grays** — Secondary text, borders, subtle backgrounds

### Typography

- **Headlines & Body:** Modern sans-serif (Inter, Source Sans Pro, or similar)
- **Code:** JetBrains Mono or Fira Code

### Visual Elements

- **Boing stripes** — Subtle diagonal stripe pattern used sparingly (hero backgrounds, section dividers, hover states)
- **Code as hero** — Novus code samples prominently displayed with syntax highlighting
- **Generous whitespace** — Modern, uncluttered
- **Dark mode** — Available from launch

### Imagery

- Minimal stock photography
- Custom illustrations/diagrams where needed
- Screenshots of real Novus code and output (added over time)
- Potential subtle Amiga hardware photography as texture

## Logo

**Direction:** Wordmark with Boing stripe accent

- Clean, modern sans-serif letterforms for "Novus"
- The "O" gets the Boing treatment — diagonal red/white stripes or sphere pattern
- Alternative: stripe element as underline or accent
- Must work in single color and at small sizes (favicon)

**Variations needed:**
- Full wordmark
- Icon only (for favicon, social cards)
- Dark and light background versions

## Site Architecture

### 1. Landing/Marketing

- **Homepage** — Hero, tagline, code samples, value props, CTAs
- **Why Novus?** — Philosophy, comparison with C/asm, the problem it solves
- **Features** — Deep-dive pages on safety, async/await, hardware DSLs, Amiga integration
- **Examples Gallery** — Curated code samples with explanations

### 2. Learning

- **Getting Started** — Installation, toolchain setup, first program
- **Tutorial** — Guided tour through the language
- **Guides** — Topic-focused how-tos

### 3. Reference

- **Language Reference** — Formal syntax and semantics
- **Standard Library** — API documentation
- **CLI Reference** — All compiler commands and options

### 4. Community

- **Download** — Installation instructions, releases
- **GitHub** — Link to repo for issues and discussions
- **Contributing** — How to get involved (for future growth)

### 5. Blog

- News and announcements
- Release notes
- Technical posts and tutorials

## Content Strategy

### Website Documentation
- Complete language coverage
- Written for accessibility and clarity
- Enough for someone to learn and use Novus productively
- Free and open

### Printed Book (Lulu)
- Premium deep-dive content
- Commentary, tips, tricks, shortcuts
- Modeled after the Amiga Programmer's Reference Guide series
- Professional reference for those who want to go deeper

## Technical Implementation

### Stack

- **Framework:** Astro 4.x (static site generation)
- **Docs:** Starlight theme (sidebar nav, search, dark mode, MDX)
- **Styling:** Tailwind CSS
- **Search:** Pagefind (static, no external service)
- **Syntax Highlighting:** Shiki (custom Novus grammar)

### Deployment

- Static build output
- Containerized (nginx or similar)
- Deployed to k8s cluster
- CI/CD via GitHub Actions

### Repository Location

```
Novus/
├── Novus/               # Compiler
├── Novus.Core/          # Core library
├── Novus.Tests/         # Tests
├── docs/                # Internal docs
├── guide/               # LaTeX book
└── website/             # Astro site
    ├── src/
    │   ├── pages/       # Landing, Why, Features
    │   ├── content/
    │   │   ├── docs/    # Documentation (Starlight)
    │   │   └── blog/    # Blog posts
    │   ├── components/  # Reusable UI
    │   └── styles/      # Global styles, Boing palette
    ├── public/          # Static assets
    └── astro.config.mjs
```

## Homepage Design

### Hero Section
- **Headline:** "New code for classic machines"
- **Subhead:** "A modern systems programming language for the Amiga 68k"
- **Animated code block:** Novus program with assembly output
- **Primary CTA:** "Get Started"
- **Secondary CTA:** "Learn More"

### Value Props (4 cards)
1. **Modern Safety** — Result types, no null, bounds checking
2. **Amiga Native** — Direct hardware access, OS integration, proper ABI
3. **Clean Syntax** — Readable, explicit, no preprocessor hell
4. **Powerful Features** — Async/await, generics, pattern matching

### Code Showcase
- Side-by-side examples showing Novus features
- Brief explanations of behavior on real Amiga hardware

## Documentation Structure

### Getting Started (15 min to working code)
- Prerequisites
- Installation (platform-specific)
- First program
- Quick syntax overview

### Tutorial (progressive learning)
- Guided project building something real
- Concepts introduced progressively
- Each chapter builds on the last

### Reference (answer any question)
- Language Reference — every keyword, operator, type
- Standard Library — every module and function
- CLI Reference — all commands and flags

### Writing Style
- Direct, concise, no fluff
- Code-first — show, then explain
- Real examples, not toy code
- Liberal cross-linking
- "See the book for deeper discussion" where appropriate

## Launch Scope

### Included in v1.0

**Landing/Marketing:**
- Homepage
- Why Novus? page
- Features overview
- Download/Install page

**Documentation:**
- Getting Started
- Language basics
- Error handling
- Memory management basics
- CLI reference

**Blog:**
- Launch announcement
- 1-2 additional posts

### Deferred to Post-Launch
- Individual feature deep-dive pages
- Full standard library reference
- Complete tutorial project
- Examples gallery
- Community/contributing pages

## Community

- **GitHub only** for now (issues, discussions)
- Add Discord/forums later if there's demand
- Keep it simple until there's a community to serve

## Open Questions

- Final logo design (concepts to explore during implementation)
- Exact color values (refine during design)
- Screenshots/videos of Novus on real hardware (add when available)
