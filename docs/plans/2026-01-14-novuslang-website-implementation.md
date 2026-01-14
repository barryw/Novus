# Novus Language Website Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build novuslang.com — the official website for the Novus programming language with landing pages and documentation.

**Architecture:** Astro static site with Starlight docs integration. Custom landing pages for marketing, Starlight-powered docs section. Tailwind for styling with a custom Boing Ball color palette.

**Tech Stack:** Astro 4.x, Starlight, Tailwind CSS, TypeScript, Pagefind search

**Design Doc:** `docs/plans/2026-01-14-novuslang-website-design.md`

---

## Phase 1: Project Setup

### Task 1: Initialize Astro Project

**Files:**
- Create: `website/` directory and Astro project

**Step 1: Create the website directory**

```bash
cd /Users/barry/RiderProjects/Novus
mkdir website
cd website
```

**Step 2: Initialize Astro with Starlight**

```bash
npm create astro@latest . -- --template starlight --install --git false --typescript strict
```

When prompted:
- Template: starlight
- Install dependencies: Yes
- TypeScript: Strict

**Step 3: Verify installation**

```bash
npm run dev
```

Expected: Dev server starts at localhost:4321, shows default Starlight page.

**Step 4: Stop dev server and commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): initialize Astro project with Starlight"
```

---

### Task 2: Add Tailwind CSS

**Files:**
- Modify: `website/package.json`
- Modify: `website/astro.config.mjs`
- Create: `website/tailwind.config.mjs`
- Create: `website/src/styles/global.css`

**Step 1: Install Tailwind and dependencies**

```bash
cd /Users/barry/RiderProjects/Novus/website
npx astro add tailwind
```

Accept all prompts (Yes to all).

**Step 2: Verify Tailwind config was created**

Check that `website/tailwind.config.mjs` exists.

**Step 3: Create global styles file**

Create `website/src/styles/global.css`:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

**Step 4: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add Tailwind CSS"
```

---

### Task 3: Configure Boing Ball Color Palette

**Files:**
- Modify: `website/tailwind.config.mjs`
- Create: `website/src/styles/boing-palette.css`

**Step 1: Update Tailwind config with Boing colors**

Replace `website/tailwind.config.mjs` with:

```javascript
import starlightPlugin from '@astrojs/starlight-tailwind';

/** @type {import('tailwindcss').Config} */
export default {
  content: ['./src/**/*.{astro,html,js,jsx,md,mdx,svelte,ts,tsx,vue}'],
  theme: {
    extend: {
      colors: {
        // Boing Ball palette
        'novus-red': {
          50: '#fef2f2',
          100: '#fee2e2',
          200: '#fecaca',
          300: '#fca5a5',
          400: '#f87171',
          500: '#E63030', // Primary Novus Red
          600: '#dc2626',
          700: '#b91c1c',
          800: '#991b1b',
          900: '#7f1d1d',
          950: '#450a0a',
        },
        // Accent for Starlight (uses CSS custom properties)
        accent: {
          DEFAULT: 'var(--sl-color-accent)',
          light: 'var(--sl-color-accent-light)',
          dark: 'var(--sl-color-accent-dark)',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'Fira Code', 'monospace'],
      },
    },
  },
  plugins: [starlightPlugin()],
};
```

**Step 2: Create Boing palette CSS custom properties**

Create `website/src/styles/boing-palette.css`:

```css
/* Boing Ball Color Palette for Novus */
:root {
  /* Primary colors */
  --novus-red: #E63030;
  --novus-white: #FFFFFF;
  --novus-black: #1A1A1A;

  /* Warm grays */
  --novus-gray-50: #FAFAF9;
  --novus-gray-100: #F5F5F4;
  --novus-gray-200: #E7E5E4;
  --novus-gray-300: #D6D3D1;
  --novus-gray-400: #A8A29E;
  --novus-gray-500: #78716C;
  --novus-gray-600: #57534E;
  --novus-gray-700: #44403C;
  --novus-gray-800: #292524;
  --novus-gray-900: #1C1917;

  /* Starlight accent overrides */
  --sl-color-accent: #E63030;
  --sl-color-accent-light: #F87171;
  --sl-color-accent-dark: #B91C1C;
  --sl-color-accent-high: #FFFFFF;
  --sl-color-accent-low: #450A0A;
}

/* Dark mode adjustments */
:root[data-theme='dark'] {
  --sl-color-accent: #F87171;
  --sl-color-accent-light: #FCA5A5;
  --sl-color-accent-dark: #E63030;
}
```

**Step 3: Import palette in global styles**

Update `website/src/styles/global.css`:

```css
@import './boing-palette.css';

@tailwind base;
@tailwind components;
@tailwind utilities;

/* Base typography */
body {
  font-family: 'Inter', system-ui, sans-serif;
}

code, pre {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
}
```

**Step 4: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add Boing Ball color palette"
```

---

### Task 4: Configure Starlight

**Files:**
- Modify: `website/astro.config.mjs`

**Step 1: Update Astro config for Novus**

Replace `website/astro.config.mjs` with:

```javascript
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import tailwind from '@astrojs/tailwind';

export default defineConfig({
  site: 'https://novuslang.com',
  integrations: [
    starlight({
      title: 'Novus',
      tagline: 'New code for classic machines',
      logo: {
        light: './src/assets/logo-light.svg',
        dark: './src/assets/logo-dark.svg',
        replacesTitle: false,
      },
      social: {
        github: 'https://github.com/barrylapthorn/novus',
      },
      sidebar: [
        {
          label: 'Getting Started',
          items: [
            { label: 'Introduction', slug: 'getting-started/introduction' },
            { label: 'Installation', slug: 'getting-started/installation' },
            { label: 'First Program', slug: 'getting-started/first-program' },
          ],
        },
        {
          label: 'Language Guide',
          items: [
            { label: 'Variables & Types', slug: 'guide/variables-types' },
            { label: 'Functions', slug: 'guide/functions' },
            { label: 'Control Flow', slug: 'guide/control-flow' },
            { label: 'Error Handling', slug: 'guide/error-handling' },
            { label: 'Memory Management', slug: 'guide/memory' },
          ],
        },
        {
          label: 'Reference',
          items: [
            { label: 'CLI Reference', slug: 'reference/cli' },
            { label: 'Language Reference', slug: 'reference/language' },
          ],
        },
      ],
      customCss: [
        './src/styles/global.css',
      ],
      head: [
        {
          tag: 'link',
          attrs: {
            rel: 'preconnect',
            href: 'https://fonts.googleapis.com',
          },
        },
        {
          tag: 'link',
          attrs: {
            rel: 'preconnect',
            href: 'https://fonts.gstatic.com',
            crossorigin: true,
          },
        },
        {
          tag: 'link',
          attrs: {
            rel: 'stylesheet',
            href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap',
          },
        },
      ],
    }),
    tailwind({ applyBaseStyles: false }),
  ],
});
```

**Step 2: Create placeholder logo files**

Create `website/src/assets/logo-light.svg`:

```svg
<svg width="32" height="32" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
  <circle cx="16" cy="16" r="14" fill="#E63030"/>
  <text x="16" y="21" text-anchor="middle" fill="white" font-family="Inter, sans-serif" font-weight="bold" font-size="14">N</text>
</svg>
```

Create `website/src/assets/logo-dark.svg`:

```svg
<svg width="32" height="32" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
  <circle cx="16" cy="16" r="14" fill="#F87171"/>
  <text x="16" y="21" text-anchor="middle" fill="white" font-family="Inter, sans-serif" font-weight="bold" font-size="14">N</text>
</svg>
```

**Step 3: Verify config**

```bash
cd /Users/barry/RiderProjects/Novus/website
npm run dev
```

Expected: Site loads with Novus branding and red accent color.

**Step 4: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): configure Starlight with Novus branding"
```

---

## Phase 2: Landing Pages

### Task 5: Create Homepage

**Files:**
- Create: `website/src/pages/index.astro`
- Create: `website/src/components/Hero.astro`
- Create: `website/src/components/ValueProps.astro`
- Create: `website/src/components/CodeShowcase.astro`
- Create: `website/src/components/Footer.astro`

**Step 1: Create Hero component**

Create `website/src/components/Hero.astro`:

```astro
---
// Hero section for homepage
---

<section class="relative overflow-hidden bg-gradient-to-b from-novus-gray-50 to-white dark:from-novus-gray-900 dark:to-novus-gray-800 py-20 px-4">
  <!-- Subtle Boing stripe pattern -->
  <div class="absolute inset-0 opacity-5">
    <div class="absolute inset-0" style="background: repeating-linear-gradient(45deg, #E63030, #E63030 10px, transparent 10px, transparent 20px);"></div>
  </div>

  <div class="relative max-w-6xl mx-auto text-center">
    <h1 class="text-5xl md:text-6xl font-bold text-novus-gray-900 dark:text-white mb-6">
      New code for <span class="text-novus-red-500">classic machines</span>
    </h1>

    <p class="text-xl md:text-2xl text-novus-gray-600 dark:text-novus-gray-300 mb-8 max-w-3xl mx-auto">
      A modern systems programming language for the Amiga 68k
    </p>

    <div class="flex flex-col sm:flex-row gap-4 justify-center">
      <a href="/getting-started/introduction" class="inline-flex items-center justify-center px-8 py-4 text-lg font-semibold text-white bg-novus-red-500 rounded-lg hover:bg-novus-red-600 transition-colors">
        Get Started
      </a>
      <a href="/why-novus" class="inline-flex items-center justify-center px-8 py-4 text-lg font-semibold text-novus-gray-700 dark:text-white bg-white dark:bg-novus-gray-700 border-2 border-novus-gray-200 dark:border-novus-gray-600 rounded-lg hover:border-novus-red-500 transition-colors">
        Learn More
      </a>
    </div>
  </div>
</section>
```

**Step 2: Create ValueProps component**

Create `website/src/components/ValueProps.astro`:

```astro
---
// Value propositions section
const props = [
  {
    title: 'Modern Safety',
    description: 'Result types, no null, bounds checking. Write confident code without the footguns.',
    icon: '🛡️',
  },
  {
    title: 'Amiga Native',
    description: 'Direct hardware access, OS integration, proper ABI. Built for Amiga, not ported.',
    icon: '🖥️',
  },
  {
    title: 'Clean Syntax',
    description: 'Readable, explicit, no preprocessor hell. Code that makes sense at a glance.',
    icon: '✨',
  },
  {
    title: 'Powerful Features',
    description: 'Async/await, generics, pattern matching. Modern tools for classic hardware.',
    icon: '⚡',
  },
];
---

<section class="py-20 px-4 bg-white dark:bg-novus-gray-800">
  <div class="max-w-6xl mx-auto">
    <h2 class="text-3xl font-bold text-center text-novus-gray-900 dark:text-white mb-12">
      Why Novus?
    </h2>

    <div class="grid md:grid-cols-2 lg:grid-cols-4 gap-8">
      {props.map((prop) => (
        <div class="p-6 rounded-xl bg-novus-gray-50 dark:bg-novus-gray-700 hover:shadow-lg transition-shadow">
          <div class="text-4xl mb-4">{prop.icon}</div>
          <h3 class="text-xl font-semibold text-novus-gray-900 dark:text-white mb-2">
            {prop.title}
          </h3>
          <p class="text-novus-gray-600 dark:text-novus-gray-300">
            {prop.description}
          </p>
        </div>
      ))}
    </div>
  </div>
</section>
```

**Step 3: Create CodeShowcase component**

Create `website/src/components/CodeShowcase.astro`:

```astro
---
// Code showcase section
---

<section class="py-20 px-4 bg-novus-gray-50 dark:bg-novus-gray-900">
  <div class="max-w-6xl mx-auto">
    <h2 class="text-3xl font-bold text-center text-novus-gray-900 dark:text-white mb-4">
      See Novus in Action
    </h2>
    <p class="text-center text-novus-gray-600 dark:text-novus-gray-400 mb-12 max-w-2xl mx-auto">
      Clean, readable code that compiles to efficient 68k assembly
    </p>

    <div class="grid lg:grid-cols-2 gap-8">
      <!-- Novus Code -->
      <div class="rounded-xl overflow-hidden shadow-lg">
        <div class="bg-novus-gray-800 px-4 py-2 flex items-center gap-2">
          <div class="w-3 h-3 rounded-full bg-novus-red-500"></div>
          <div class="w-3 h-3 rounded-full bg-yellow-500"></div>
          <div class="w-3 h-3 rounded-full bg-green-500"></div>
          <span class="ml-2 text-novus-gray-400 text-sm font-mono">hello.novus</span>
        </div>
        <pre class="bg-novus-gray-900 p-6 overflow-x-auto"><code class="text-sm font-mono text-novus-gray-100"><span class="text-novus-red-400">fn</span> <span class="text-blue-400">main</span>() -> <span class="text-green-400">u32</span> {
    <span class="text-purple-400">let</span> message = <span class="text-yellow-300">"Hello, Amiga!"</span>
    print(message)
    <span class="text-novus-red-400">return</span> <span class="text-orange-400">0</span>
}</code></pre>
      </div>

      <!-- Generated Assembly -->
      <div class="rounded-xl overflow-hidden shadow-lg">
        <div class="bg-novus-gray-800 px-4 py-2 flex items-center gap-2">
          <div class="w-3 h-3 rounded-full bg-novus-red-500"></div>
          <div class="w-3 h-3 rounded-full bg-yellow-500"></div>
          <div class="w-3 h-3 rounded-full bg-green-500"></div>
          <span class="ml-2 text-novus-gray-400 text-sm font-mono">Generated 68k Assembly</span>
        </div>
        <pre class="bg-novus-gray-900 p-6 overflow-x-auto"><code class="text-sm font-mono text-novus-gray-100"><span class="text-novus-gray-500">; Clean, readable output</span>
<span class="text-blue-400">_main:</span>
    <span class="text-green-400">lea</span>     _str_hello,a0
    <span class="text-green-400">jsr</span>     _print
    <span class="text-green-400">moveq</span>   #0,d0
    <span class="text-green-400">rts</span>

<span class="text-blue-400">_str_hello:</span>
    <span class="text-green-400">dc.b</span>    <span class="text-yellow-300">"Hello, Amiga!",0</span></code></pre>
      </div>
    </div>

    <p class="text-center text-novus-gray-500 dark:text-novus-gray-400 mt-8 text-sm">
      On a real Amiga, this opens a console window and prints the message.
    </p>
  </div>
</section>
```

**Step 4: Create Footer component**

Create `website/src/components/Footer.astro`:

```astro
---
// Site footer
const currentYear = new Date().getFullYear();
---

<footer class="bg-novus-gray-900 text-novus-gray-400 py-12 px-4">
  <div class="max-w-6xl mx-auto">
    <div class="grid md:grid-cols-4 gap-8 mb-8">
      <!-- Brand -->
      <div>
        <h3 class="text-white font-bold text-lg mb-4">Novus</h3>
        <p class="text-sm">
          New code for classic machines. A modern systems programming language for the Amiga 68k.
        </p>
      </div>

      <!-- Learn -->
      <div>
        <h4 class="text-white font-semibold mb-4">Learn</h4>
        <ul class="space-y-2 text-sm">
          <li><a href="/getting-started/introduction" class="hover:text-white transition-colors">Getting Started</a></li>
          <li><a href="/guide/variables-types" class="hover:text-white transition-colors">Language Guide</a></li>
          <li><a href="/reference/cli" class="hover:text-white transition-colors">Reference</a></li>
        </ul>
      </div>

      <!-- Community -->
      <div>
        <h4 class="text-white font-semibold mb-4">Community</h4>
        <ul class="space-y-2 text-sm">
          <li><a href="https://github.com/barrylapthorn/novus" class="hover:text-white transition-colors">GitHub</a></li>
          <li><a href="/blog" class="hover:text-white transition-colors">Blog</a></li>
        </ul>
      </div>

      <!-- Resources -->
      <div>
        <h4 class="text-white font-semibold mb-4">Resources</h4>
        <ul class="space-y-2 text-sm">
          <li><a href="/why-novus" class="hover:text-white transition-colors">Why Novus?</a></li>
          <li><a href="/features" class="hover:text-white transition-colors">Features</a></li>
        </ul>
      </div>
    </div>

    <div class="border-t border-novus-gray-800 pt-8 text-center text-sm">
      <p>Made for the Amiga community · {currentYear}</p>
    </div>
  </div>
</footer>
```

**Step 5: Create Homepage**

Create `website/src/pages/index.astro`:

```astro
---
import Hero from '../components/Hero.astro';
import ValueProps from '../components/ValueProps.astro';
import CodeShowcase from '../components/CodeShowcase.astro';
import Footer from '../components/Footer.astro';
---

<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Novus - New code for classic machines</title>
    <meta name="description" content="A modern systems programming language for the Amiga 68k" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet" />
    <style>
      @import '../styles/global.css';
    </style>
  </head>
  <body class="bg-white dark:bg-novus-gray-900">
    <!-- Navigation -->
    <nav class="fixed top-0 left-0 right-0 z-50 bg-white/80 dark:bg-novus-gray-900/80 backdrop-blur-sm border-b border-novus-gray-200 dark:border-novus-gray-700">
      <div class="max-w-6xl mx-auto px-4 py-4 flex items-center justify-between">
        <a href="/" class="text-2xl font-bold text-novus-gray-900 dark:text-white">
          N<span class="text-novus-red-500">o</span>vus
        </a>
        <div class="flex items-center gap-6">
          <a href="/getting-started/introduction" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Docs</a>
          <a href="/why-novus" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Why Novus?</a>
          <a href="/blog" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Blog</a>
          <a href="https://github.com/barrylapthorn/novus" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">GitHub</a>
        </div>
      </div>
    </nav>

    <!-- Add padding for fixed nav -->
    <div class="pt-16">
      <Hero />
      <ValueProps />
      <CodeShowcase />
    </div>

    <Footer />
  </body>
</html>
```

**Step 6: Verify homepage**

```bash
cd /Users/barry/RiderProjects/Novus/website
npm run dev
```

Visit http://localhost:4321 - should see the homepage with hero, value props, and code showcase.

**Step 7: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add homepage with hero, value props, and code showcase"
```

---

### Task 6: Create Why Novus Page

**Files:**
- Create: `website/src/pages/why-novus.astro`

**Step 1: Create Why Novus page**

Create `website/src/pages/why-novus.astro`:

```astro
---
import Footer from '../components/Footer.astro';
---

<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Why Novus? - Novus Programming Language</title>
    <meta name="description" content="Why choose Novus for Amiga development? Modern safety, clean syntax, and native Amiga integration." />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet" />
    <style>
      @import '../styles/global.css';
    </style>
  </head>
  <body class="bg-white dark:bg-novus-gray-900">
    <!-- Navigation -->
    <nav class="fixed top-0 left-0 right-0 z-50 bg-white/80 dark:bg-novus-gray-900/80 backdrop-blur-sm border-b border-novus-gray-200 dark:border-novus-gray-700">
      <div class="max-w-6xl mx-auto px-4 py-4 flex items-center justify-between">
        <a href="/" class="text-2xl font-bold text-novus-gray-900 dark:text-white">
          N<span class="text-novus-red-500">o</span>vus
        </a>
        <div class="flex items-center gap-6">
          <a href="/getting-started/introduction" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Docs</a>
          <a href="/why-novus" class="text-novus-red-500 font-semibold">Why Novus?</a>
          <a href="/blog" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Blog</a>
          <a href="https://github.com/barrylapthorn/novus" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">GitHub</a>
        </div>
      </div>
    </nav>

    <main class="pt-24 pb-20 px-4">
      <article class="max-w-3xl mx-auto">
        <h1 class="text-4xl md:text-5xl font-bold text-novus-gray-900 dark:text-white mb-6">
          Why Novus?
        </h1>

        <p class="text-xl text-novus-gray-600 dark:text-novus-gray-300 mb-12">
          If you're developing for the Amiga today, you have two main choices: C with all its footguns, or raw assembly. Novus offers a third path.
        </p>

        <section class="mb-12">
          <h2 class="text-2xl font-bold text-novus-gray-900 dark:text-white mb-4">The Problem with C</h2>
          <p class="text-novus-gray-600 dark:text-novus-gray-300 mb-4">
            C is the lingua franca of Amiga development, but it comes with baggage:
          </p>
          <ul class="list-disc list-inside text-novus-gray-600 dark:text-novus-gray-300 space-y-2 mb-4">
            <li>Null pointer dereferences crash your Amiga (no protected memory!)</li>
            <li>Buffer overflows corrupt memory silently</li>
            <li>The preprocessor creates unreadable, hard-to-debug code</li>
            <li>Header file management is tedious</li>
            <li>Error handling is an afterthought (return codes, errno, setjmp...)</li>
          </ul>
          <p class="text-novus-gray-600 dark:text-novus-gray-300">
            On a system without memory protection, these aren't just bugs—they're system-crashing catastrophes.
          </p>
        </section>

        <section class="mb-12">
          <h2 class="text-2xl font-bold text-novus-gray-900 dark:text-white mb-4">The Problem with Assembly</h2>
          <p class="text-novus-gray-600 dark:text-novus-gray-300 mb-4">
            Assembly gives you total control, but at a cost:
          </p>
          <ul class="list-disc list-inside text-novus-gray-600 dark:text-novus-gray-300 space-y-2">
            <li>Verbose—simple operations take many lines</li>
            <li>Easy to make register allocation mistakes</li>
            <li>No type checking at all</li>
            <li>Difficult to maintain and refactor</li>
            <li>Steep learning curve for new Amiga developers</li>
          </ul>
        </section>

        <section class="mb-12">
          <h2 class="text-2xl font-bold text-novus-gray-900 dark:text-white mb-4">The Novus Approach</h2>
          <p class="text-novus-gray-600 dark:text-novus-gray-300 mb-4">
            Novus takes a different path:
          </p>

          <div class="space-y-6">
            <div class="p-6 bg-novus-gray-50 dark:bg-novus-gray-800 rounded-lg">
              <h3 class="font-semibold text-novus-gray-900 dark:text-white mb-2">Safety Without Overhead</h3>
              <p class="text-novus-gray-600 dark:text-novus-gray-300">
                Result and Option types make error handling explicit. No null pointers. Bounds checking in debug builds catches mistakes before they crash your machine.
              </p>
            </div>

            <div class="p-6 bg-novus-gray-50 dark:bg-novus-gray-800 rounded-lg">
              <h3 class="font-semibold text-novus-gray-900 dark:text-white mb-2">Clean, Modern Syntax</h3>
              <p class="text-novus-gray-600 dark:text-novus-gray-300">
                No preprocessor. No header files. Type inference reduces boilerplate. Pattern matching makes complex logic readable.
              </p>
            </div>

            <div class="p-6 bg-novus-gray-50 dark:bg-novus-gray-800 rounded-lg">
              <h3 class="font-semibold text-novus-gray-900 dark:text-white mb-2">Amiga Native</h3>
              <p class="text-novus-gray-600 dark:text-novus-gray-300">
                Not a port of something else. Novus is designed for Amiga from the ground up: proper ABI compliance, direct hardware access, OS library integration, and async primitives built on Exec signals.
              </p>
            </div>

            <div class="p-6 bg-novus-gray-50 dark:bg-novus-gray-800 rounded-lg">
              <h3 class="font-semibold text-novus-gray-900 dark:text-white mb-2">Readable Output</h3>
              <p class="text-novus-gray-600 dark:text-novus-gray-300">
                Novus generates clean, readable 68k assembly. When you need to debug at the metal, you can actually understand what the compiler produced.
              </p>
            </div>
          </div>
        </section>

        <section class="mb-12">
          <h2 class="text-2xl font-bold text-novus-gray-900 dark:text-white mb-4">Philosophy</h2>
          <ul class="space-y-4">
            <li class="flex gap-4">
              <span class="text-novus-red-500 font-bold">1.</span>
              <div>
                <span class="font-semibold text-novus-gray-900 dark:text-white">Explicit over implicit</span>
                <span class="text-novus-gray-600 dark:text-novus-gray-300"> — No hidden allocations, no magic. You know what your code does.</span>
              </div>
            </li>
            <li class="flex gap-4">
              <span class="text-novus-red-500 font-bold">2.</span>
              <div>
                <span class="font-semibold text-novus-gray-900 dark:text-white">Predictable performance</span>
                <span class="text-novus-gray-600 dark:text-novus-gray-300"> — No garbage collector, no runtime surprises. Deterministic execution.</span>
              </div>
            </li>
            <li class="flex gap-4">
              <span class="text-novus-red-500 font-bold">3.</span>
              <div>
                <span class="font-semibold text-novus-gray-900 dark:text-white">Respect the machine</span>
                <span class="text-novus-gray-600 dark:text-novus-gray-300"> — Leverage the Amiga's architecture instead of hiding it.</span>
              </div>
            </li>
            <li class="flex gap-4">
              <span class="text-novus-red-500 font-bold">4.</span>
              <div>
                <span class="font-semibold text-novus-gray-900 dark:text-white">Amiga first</span>
                <span class="text-novus-gray-600 dark:text-novus-gray-300"> — Not cross-platform. Focused entirely on authentic Amiga development.</span>
              </div>
            </li>
          </ul>
        </section>

        <div class="text-center">
          <a href="/getting-started/introduction" class="inline-flex items-center justify-center px-8 py-4 text-lg font-semibold text-white bg-novus-red-500 rounded-lg hover:bg-novus-red-600 transition-colors">
            Get Started with Novus
          </a>
        </div>
      </article>
    </main>

    <Footer />
  </body>
</html>
```

**Step 2: Verify page**

```bash
cd /Users/barry/RiderProjects/Novus/website
npm run dev
```

Visit http://localhost:4321/why-novus - should see the Why Novus page.

**Step 3: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add Why Novus page"
```

---

## Phase 3: Documentation Content

### Task 7: Create Getting Started Documentation

**Files:**
- Create: `website/src/content/docs/getting-started/introduction.md`
- Create: `website/src/content/docs/getting-started/installation.md`
- Create: `website/src/content/docs/getting-started/first-program.md`

**Step 1: Create Introduction page**

Create `website/src/content/docs/getting-started/introduction.md`:

```markdown
---
title: Introduction
description: Welcome to Novus, a modern systems programming language for the Amiga 68k.
---

# Welcome to Novus

**Novus** is a modern systems programming language designed specifically for the Amiga 68k family of computers. It combines modern language features with direct hardware access and the efficiency required for 68k systems.

## What is Novus?

Novus is:

- **A compiled language** — Your code compiles to native 68k machine code
- **Systems-level** — Direct hardware access, no runtime overhead
- **Safe by default** — Result types, bounds checking, no null pointers
- **Amiga-native** — Built for AmigaOS, not ported from elsewhere

## Who is Novus for?

Novus is designed for:

- **Amiga enthusiasts** who want to write new software for classic hardware
- **Retro developers** looking for a more modern development experience
- **Anyone** tired of C's footguns or assembly's verbosity

## Current Status

Novus is in active development. The compiler produces working Amiga executables and supports:

- Core language features (variables, functions, control flow)
- Strong typing with type inference
- Result and Option types for error handling
- CPU-target aware code generation (68000–68060)
- AmigaOS library integration

See the [GitHub repository](https://github.com/barrylapthorn/novus) for the latest development status.

## Next Steps

Ready to try Novus?

1. [Install the toolchain](/getting-started/installation)
2. [Write your first program](/getting-started/first-program)
3. [Learn the language basics](/guide/variables-types)
```

**Step 2: Create Installation page**

Create `website/src/content/docs/getting-started/installation.md`:

```markdown
---
title: Installation
description: How to install the Novus compiler and toolchain.
---

# Installation

This guide will help you set up the Novus compiler and its dependencies.

## Prerequisites

Before installing Novus, you'll need:

### 1. .NET 9.0 SDK

The Novus compiler is written in C# and requires .NET 9.0.

**macOS (Homebrew):**
```bash
brew install dotnet@9
```

**Linux:**
Follow the [official .NET installation guide](https://learn.microsoft.com/en-us/dotnet/core/install/linux).

**Windows:**
Download from the [.NET website](https://dotnet.microsoft.com/download/dotnet/9.0).

### 2. VBCC Toolchain

Novus uses VBCC's assembler (vasm) and linker (vlink) to produce Amiga executables.

Download VBCC from [http://www.compilers.de/vbcc.html](http://www.compilers.de/vbcc.html) and follow the installation instructions for your platform.

Set the `VBCC` environment variable to your VBCC installation directory:

```bash
export VBCC=/path/to/vbcc
```

### 3. Amiga NDK 3.9

The NDK provides AmigaOS header files and libraries.

Download from [Hyperion Entertainment](https://www.hyperion-entertainment.com/) or find it in the Amiga community archives.

## Installing Novus

### From Source

Clone the repository and build:

```bash
git clone https://github.com/barrylapthorn/novus.git
cd novus
dotnet build
```

### Verify Installation

Test that everything works:

```bash
dotnet run --project Novus -- --version
```

You should see the Novus version information.

## Running on Amiga

To run compiled programs, you'll need:

- A real Amiga (any model with 68000 or higher)
- An Amiga emulator (WinUAE, FS-UAE, vAmiga)

Transfer your compiled executables to the Amiga via:
- Shared folders (emulator)
- Network transfer
- Floppy disk or CF card

## Next Steps

Your environment is ready! Continue to [Your First Program](/getting-started/first-program).
```

**Step 3: Create First Program page**

Create `website/src/content/docs/getting-started/first-program.md`:

```markdown
---
title: Your First Program
description: Write, compile, and run your first Novus program.
---

# Your First Program

Let's write a simple Novus program and run it on an Amiga (or emulator).

## Hello, Amiga!

Create a file called `hello.novus`:

```novus
fn main() -> u32 {
    let message = "Hello, Amiga!"
    print(message)
    return 0
}
```

This program:
1. Defines a `main` function that returns a `u32` (the exit code)
2. Creates a string variable `message`
3. Prints the message to the console
4. Returns 0 (success)

## Compiling

Compile your program with:

```bash
dotnet run --project Novus -- compile hello.novus -o hello
```

This produces an Amiga executable called `hello`.

### Compiler Options

Common options:

| Option | Description |
|--------|-------------|
| `-o <file>` | Output file name |
| `--cpu <target>` | Target CPU: 68000, 68020, 68030, 68040, 68060 |
| `--emit-asm` | Output assembly instead of linking |
| `-O <level>` | Optimization level (0-3) |
| `-v` | Verbose output |

Example for 68020 with optimization:

```bash
dotnet run --project Novus -- compile hello.novus -o hello --cpu 68020 -O 2
```

## Running

### On an Emulator

1. Copy `hello` to your emulator's shared drive
2. Open a Shell on the Amiga
3. Run: `hello`

You should see:
```
Hello, Amiga!
```

### On Real Hardware

Transfer the executable to your Amiga via network, floppy, or CF card, then run it from the Shell.

## Understanding the Output

If you're curious about the generated code, use `--emit-asm`:

```bash
dotnet run --project Novus -- compile hello.novus --emit-asm -o hello.s
```

You'll see clean 68k assembly:

```asm
    section text,code

    xdef _main
_main:
    lea     _str_hello,a0
    jsr     _print
    moveq   #0,d0
    rts

_str_hello:
    dc.b    "Hello, Amiga!",0
```

## Next Steps

Now that you've run your first program:

- [Learn about variables and types](/guide/variables-types)
- [Explore functions](/guide/functions)
- [Understand error handling](/guide/error-handling)
```

**Step 4: Verify docs**

```bash
cd /Users/barry/RiderProjects/Novus/website
npm run dev
```

Visit http://localhost:4321/getting-started/introduction - should see the docs with sidebar navigation.

**Step 5: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add Getting Started documentation"
```

---

### Task 8: Create Language Guide Stubs

**Files:**
- Create: `website/src/content/docs/guide/variables-types.md`
- Create: `website/src/content/docs/guide/functions.md`
- Create: `website/src/content/docs/guide/control-flow.md`
- Create: `website/src/content/docs/guide/error-handling.md`
- Create: `website/src/content/docs/guide/memory.md`

**Step 1: Create Variables & Types page**

Create `website/src/content/docs/guide/variables-types.md`:

```markdown
---
title: Variables & Types
description: Learn about variables, type inference, and Novus's type system.
---

# Variables & Types

Novus is a statically-typed language with type inference. This means the compiler checks types at compile time, but you don't always have to write them explicitly.

## Variable Declarations

Use `let` to declare variables:

```novus
let x = 42          // Type inferred as i32
let y: u16 = 100    // Explicit type annotation
```

### Mutability

Variables are immutable by default. Use `mut` for mutable variables:

```novus
let x = 10          // Immutable
let mut y = 20      // Mutable
y = 30              // OK
// x = 15           // Error: cannot assign to immutable variable
```

## Primitive Types

### Integers

| Type | Size | Range |
|------|------|-------|
| `i8` | 8-bit | -128 to 127 |
| `i16` | 16-bit | -32,768 to 32,767 |
| `i32` | 32-bit | -2³¹ to 2³¹-1 |
| `i64` | 64-bit | -2⁶³ to 2⁶³-1 |
| `u8` | 8-bit | 0 to 255 |
| `u16` | 16-bit | 0 to 65,535 |
| `u32` | 32-bit | 0 to 2³²-1 |
| `u64` | 64-bit | 0 to 2⁶⁴-1 |

```novus
let byte: u8 = 255
let word: u16 = 0xFFFF
let long: u32 = 1_000_000  // Underscores for readability
```

### Boolean

```novus
let flag: bool = true
let done = false
```

### Characters and Strings

```novus
let ch: char = 'A'
let message: str = "Hello, Amiga!"
```

## Type Inference

The compiler infers types from context:

```novus
let a = 42          // i32 (default for integer literals)
let b = 3.14        // f32 (default for float literals)
let c = true        // bool
let d = "hello"     // str
```

You can guide inference with suffixes or annotations:

```novus
let x = 42u16       // u16
let y = 100i64      // i64
let z: u8 = 50      // u8
```

## Arrays

Fixed-size arrays:

```novus
let numbers: [i32; 5] = [1, 2, 3, 4, 5]
let zeros: [u8; 100] = [0; 100]  // 100 zeros
```

Access elements with indexing:

```novus
let first = numbers[0]
let last = numbers[4]
```

## Next Steps

- [Functions](/guide/functions) — Learn how to define and call functions
- [Control Flow](/guide/control-flow) — Conditionals and loops
```

**Step 2: Create remaining guide stubs**

Create `website/src/content/docs/guide/functions.md`:

```markdown
---
title: Functions
description: Defining and calling functions in Novus.
---

# Functions

Functions are the building blocks of Novus programs.

## Defining Functions

```novus
fn add(a: i32, b: i32) -> i32 {
    return a + b
}
```

Components:
- `fn` keyword
- Function name (`add`)
- Parameters with types (`a: i32, b: i32`)
- Return type (`-> i32`)
- Body in braces

## Calling Functions

```novus
let result = add(10, 20)  // result = 30
```

## Return Values

The `return` keyword exits the function with a value:

```novus
fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    }
    return b
}
```

## Functions Without Return Values

Use `-> void` or omit the return type:

```novus
fn greet(name: str) {
    print("Hello, ")
    print(name)
}
```

## Early Returns

Return early from functions:

```novus
fn find_first_even(numbers: [i32; 10]) -> Option[i32] {
    for n in numbers {
        if n % 2 == 0 {
            return Some(n)
        }
    }
    return None
}
```

## Next Steps

- [Control Flow](/guide/control-flow) — Conditionals and loops
- [Error Handling](/guide/error-handling) — Result and Option types
```

Create `website/src/content/docs/guide/control-flow.md`:

```markdown
---
title: Control Flow
description: Conditionals, loops, and pattern matching in Novus.
---

# Control Flow

Novus provides familiar control flow constructs with some modern additions.

## Conditionals

### if/else

```novus
if score >= 90 {
    print("A")
} else if score >= 80 {
    print("B")
} else {
    print("C")
}
```

### if as Expression

`if` can return a value:

```novus
let grade = if score >= 60 { "Pass" } else { "Fail" }
```

## Loops

### while

```novus
let mut i = 0
while i < 10 {
    print(i)
    i = i + 1
}
```

### for

Iterate over ranges:

```novus
for i in 0..10 {
    print(i)
}
```

Iterate over arrays:

```novus
let items = [1, 2, 3, 4, 5]
for item in items {
    print(item)
}
```

### loop

Infinite loop (use `break` to exit):

```novus
loop {
    let input = read_input()
    if input == "quit" {
        break
    }
    process(input)
}
```

## Pattern Matching

The `match` expression handles multiple cases:

```novus
match value {
    0 => print("zero"),
    1 => print("one"),
    2..=9 => print("single digit"),
    _ => print("large number"),
}
```

### Matching on Option

```novus
match find_user(id) {
    Some(user) => print(user.name),
    None => print("User not found"),
}
```

### Matching on Result

```novus
match open_file(path) {
    Ok(file) => process(file),
    Err(e) => print("Error: ", e),
}
```

## Next Steps

- [Error Handling](/guide/error-handling) — Result and Option in depth
- [Memory Management](/guide/memory) — Understanding memory in Novus
```

Create `website/src/content/docs/guide/error-handling.md`:

```markdown
---
title: Error Handling
description: Using Result and Option types for safe error handling.
---

# Error Handling

Novus uses `Result` and `Option` types instead of exceptions or null pointers. This makes error handling explicit and prevents crashes.

## The Problem with Null

In C, functions often return null to indicate failure:

```c
// C code - dangerous!
FILE *f = fopen("data.txt", "r");
fread(buffer, 1, 100, f);  // Crash if f is NULL!
```

On the Amiga (with no memory protection), this crashes the entire system.

## Option Type

`Option[T]` represents a value that may or may not exist:

```novus
enum Option[T] {
    Some(T),
    None,
}
```

Use it when something might not be present:

```novus
fn find_user(id: u32) -> Option[User] {
    // Returns Some(user) if found, None otherwise
}

match find_user(42) {
    Some(user) => print(user.name),
    None => print("User not found"),
}
```

## Result Type

`Result[T, E]` represents an operation that can succeed or fail:

```novus
enum Result[T, E] {
    Ok(T),
    Err(E),
}
```

Use it for operations that can fail:

```novus
fn open_file(path: str) -> Result[File, DosError] {
    // Returns Ok(file) on success, Err(error) on failure
}

match open_file("data.txt") {
    Ok(file) => process(file),
    Err(e) => print("Failed to open: ", e),
}
```

## The ? Operator

Propagate errors concisely with `?`:

```novus
fn read_config() -> Result[Config, DosError] {
    let file = open_file("config.txt")?  // Returns early on error
    let data = read_all(file)?
    let config = parse_config(data)?
    return Ok(config)
}
```

## Unwrapping

When you're certain a value exists, use `unwrap()`:

```novus
let value = some_option.unwrap()  // Panics if None!
```

**Use sparingly!** Prefer pattern matching or `?` for safety.

## Next Steps

- [Memory Management](/guide/memory) — How Novus handles memory
- [CLI Reference](/reference/cli) — Compiler options
```

Create `website/src/content/docs/guide/memory.md`:

```markdown
---
title: Memory Management
description: Understanding memory allocation and ownership in Novus.
---

# Memory Management

Novus gives you control over memory without the footguns of C.

## Stack vs Heap

**Stack allocation** is automatic for local variables:

```novus
fn example() {
    let x = 42           // On stack
    let arr = [0; 100]   // On stack (if size is known)
}  // Automatically freed when function returns
```

**Heap allocation** is explicit:

```novus
let buffer = Vec::with_capacity(1024)?  // Heap allocated
```

## Ownership

Every value has a single owner. When the owner goes out of scope, the value is freed:

```novus
fn example() {
    let data = Vec::new()  // data owns the Vec
    // ... use data ...
}  // data goes out of scope, Vec is freed
```

## Borrowing

Pass references to avoid copying:

```novus
fn print_length(v: &Vec[i32]) {
    print(v.len())
}

let numbers = vec![1, 2, 3]
print_length(&numbers)  // Borrow, don't move
// numbers is still valid here
```

## Mutable Borrows

Mutate borrowed data with `&mut`:

```novus
fn add_one(v: &mut Vec[i32]) {
    v.push(1)
}

let mut numbers = vec![1, 2, 3]
add_one(&mut numbers)
```

## AmigaOS Memory

For OS-level allocation (Chip RAM, specific memory types):

```novus
use std::os::exec::{AllocMem, FreeMem, MEMF_CHIP}

let chip_buffer = AllocMem(1024, MEMF_CHIP)?
// ... use buffer ...
FreeMem(chip_buffer, 1024)
```

## RAII and defer

Use `defer` for guaranteed cleanup:

```novus
fn process_file(path: str) -> Result[(), DosError] {
    let file = open_file(path)?
    defer close_file(file)  // Always runs, even on error

    // ... process file ...
    return Ok(())
}
```

## Next Steps

- [CLI Reference](/reference/cli) — Compiler command reference
- [Language Reference](/reference/language) — Complete language specification
```

**Step 3: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add Language Guide documentation"
```

---

### Task 9: Create Reference Documentation

**Files:**
- Create: `website/src/content/docs/reference/cli.md`
- Create: `website/src/content/docs/reference/language.md`

**Step 1: Create CLI Reference**

Create `website/src/content/docs/reference/cli.md`:

```markdown
---
title: CLI Reference
description: Complete reference for the Novus compiler command-line interface.
---

# CLI Reference

The Novus compiler (`novus`) is a command-line tool for compiling Novus source code to Amiga executables.

## Basic Usage

```bash
novus compile <input.novus> [options]
```

Or when running from source:

```bash
dotnet run --project Novus -- compile <input.novus> [options]
```

## Commands

### compile

Compile a Novus source file to an Amiga executable.

```bash
novus compile hello.novus -o hello
```

### check

Type-check without generating code:

```bash
novus check hello.novus
```

### build

Build a workspace or project:

```bash
novus build
```

## Options

### Output Options

| Option | Description |
|--------|-------------|
| `-o <file>` | Output file name (default: `a.out`) |
| `--emit-asm` | Output assembly instead of executable |
| `--emit-ir` | Output intermediate representation |

### Target Options

| Option | Description |
|--------|-------------|
| `--cpu <target>` | Target CPU (see below) |
| `--chipset <type>` | Target chipset: `ocs`, `ecs`, `aga`, `auto` |

**CPU Targets:**

| Target | Description |
|--------|-------------|
| `68000` | Base 68000 (default) |
| `68010` | 68010 |
| `68020` | 68020 (32-bit multiply/divide) |
| `68030` | 68030 |
| `68040` | 68040 (FPU) |
| `68060` | 68060 (superscalar) |

### Optimization Options

| Option | Description |
|--------|-------------|
| `-O 0` | No optimization |
| `-O 1` | Basic optimization |
| `-O 2` | Standard optimization (recommended) |
| `-O 3` | Aggressive optimization |

### Toolchain Options

| Option | Description |
|--------|-------------|
| `--vbcc-path <path>` | Path to VBCC installation |
| `--ndk-path <path>` | Path to Amiga NDK |

### Debugging Options

| Option | Description |
|--------|-------------|
| `-v, --verbose` | Verbose output |
| `--debug` | Include debug symbols |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `VBCC` | Path to VBCC installation |
| `NDK_PATH` | Path to Amiga NDK |

## Examples

**Basic compilation:**
```bash
novus compile game.novus -o game
```

**Target 68020 with optimization:**
```bash
novus compile game.novus -o game --cpu 68020 -O 2
```

**View generated assembly:**
```bash
novus compile game.novus --emit-asm -o game.s
```

**Verbose output for debugging:**
```bash
novus compile game.novus -o game -v
```
```

**Step 2: Create Language Reference stub**

Create `website/src/content/docs/reference/language.md`:

```markdown
---
title: Language Reference
description: Formal reference for the Novus programming language.
---

# Language Reference

This is the formal reference for the Novus programming language. For a gentler introduction, see the [Language Guide](/guide/variables-types).

## Lexical Structure

### Comments

```novus
// Single-line comment

/*
   Multi-line
   comment
*/
```

### Identifiers

Identifiers start with a letter or underscore, followed by letters, digits, or underscores:

```
identifier = [a-zA-Z_][a-zA-Z0-9_]*
```

### Keywords

```
fn, let, mut, if, else, while, for, loop, match, return,
break, continue, struct, enum, impl, trait, pub, use,
from, import, as, true, false, self, Self, defer, unsafe,
async, await, in, where
```

### Literals

**Integers:**
```novus
42        // Decimal
0xFF      // Hexadecimal
0o77      // Octal
0b1010    // Binary
1_000_000 // With separators
```

**Floats:**
```novus
3.14
2.5e10
```

**Strings:**
```novus
"Hello, world!"
"Line 1\nLine 2"  // Escape sequences
```

**Characters:**
```novus
'a'
'\n'
```

## Types

### Primitive Types

| Type | Description |
|------|-------------|
| `bool` | Boolean (`true` or `false`) |
| `i8`, `i16`, `i32`, `i64` | Signed integers |
| `u8`, `u16`, `u32`, `u64` | Unsigned integers |
| `f32`, `f64` | Floating-point |
| `char` | Unicode character |
| `str` | String slice |

### Compound Types

**Arrays:** `[T; N]` — Fixed-size array of `N` elements of type `T`

**Tuples:** `(T1, T2, ...)` — Fixed-size heterogeneous collection

**Slices:** `[T]` — Dynamically-sized view into contiguous sequence

### User-Defined Types

**Structs:**
```novus
struct Point {
    x: i32,
    y: i32,
}
```

**Enums:**
```novus
enum Option[T] {
    Some(T),
    None,
}
```

## Expressions

*[Full expression grammar to be documented]*

## Statements

*[Full statement grammar to be documented]*

## Functions

*[Full function syntax to be documented]*

## Modules

*[Module system to be documented]*

---

*This reference is a work in progress. See the [Language Guide](/guide/variables-types) for practical documentation.*
```

**Step 3: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add Reference documentation"
```

---

## Phase 4: Blog Setup

### Task 10: Create Blog Infrastructure

**Files:**
- Create: `website/src/content/config.ts`
- Create: `website/src/pages/blog/index.astro`
- Create: `website/src/pages/blog/[slug].astro`
- Create: `website/src/content/blog/2026-01-14-introducing-novus.md`

**Step 1: Configure content collections**

Create `website/src/content/config.ts`:

```typescript
import { defineCollection, z } from 'astro:content';
import { docsSchema } from '@astrojs/starlight/schema';

const blog = defineCollection({
  type: 'content',
  schema: z.object({
    title: z.string(),
    description: z.string(),
    date: z.date(),
    author: z.string().default('Barry Lapthorn'),
    tags: z.array(z.string()).optional(),
  }),
});

export const collections = {
  docs: defineCollection({ schema: docsSchema() }),
  blog,
};
```

**Step 2: Create blog index page**

Create `website/src/pages/blog/index.astro`:

```astro
---
import { getCollection } from 'astro:content';
import Footer from '../../components/Footer.astro';

const posts = (await getCollection('blog')).sort(
  (a, b) => b.data.date.valueOf() - a.data.date.valueOf()
);
---

<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Blog - Novus Programming Language</title>
    <meta name="description" content="News, announcements, and technical articles about the Novus programming language." />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet" />
    <style>
      @import '../../styles/global.css';
    </style>
  </head>
  <body class="bg-white dark:bg-novus-gray-900">
    <!-- Navigation -->
    <nav class="fixed top-0 left-0 right-0 z-50 bg-white/80 dark:bg-novus-gray-900/80 backdrop-blur-sm border-b border-novus-gray-200 dark:border-novus-gray-700">
      <div class="max-w-6xl mx-auto px-4 py-4 flex items-center justify-between">
        <a href="/" class="text-2xl font-bold text-novus-gray-900 dark:text-white">
          N<span class="text-novus-red-500">o</span>vus
        </a>
        <div class="flex items-center gap-6">
          <a href="/getting-started/introduction" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Docs</a>
          <a href="/why-novus" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Why Novus?</a>
          <a href="/blog" class="text-novus-red-500 font-semibold">Blog</a>
          <a href="https://github.com/barrylapthorn/novus" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">GitHub</a>
        </div>
      </div>
    </nav>

    <main class="pt-24 pb-20 px-4">
      <div class="max-w-3xl mx-auto">
        <h1 class="text-4xl font-bold text-novus-gray-900 dark:text-white mb-8">Blog</h1>

        <div class="space-y-8">
          {posts.map((post) => (
            <article class="border-b border-novus-gray-200 dark:border-novus-gray-700 pb-8">
              <a href={`/blog/${post.slug}`} class="block group">
                <h2 class="text-2xl font-semibold text-novus-gray-900 dark:text-white group-hover:text-novus-red-500 transition-colors mb-2">
                  {post.data.title}
                </h2>
                <p class="text-novus-gray-600 dark:text-novus-gray-400 mb-2">
                  {post.data.description}
                </p>
                <time class="text-sm text-novus-gray-500">
                  {post.data.date.toLocaleDateString('en-US', {
                    year: 'numeric',
                    month: 'long',
                    day: 'numeric'
                  })}
                </time>
              </a>
            </article>
          ))}
        </div>
      </div>
    </main>

    <Footer />
  </body>
</html>
```

**Step 3: Create blog post template**

Create `website/src/pages/blog/[slug].astro`:

```astro
---
import { getCollection } from 'astro:content';
import Footer from '../../components/Footer.astro';

export async function getStaticPaths() {
  const posts = await getCollection('blog');
  return posts.map((post) => ({
    params: { slug: post.slug },
    props: { post },
  }));
}

const { post } = Astro.props;
const { Content } = await post.render();
---

<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{post.data.title} - Novus Blog</title>
    <meta name="description" content={post.data.description} />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet" />
    <style>
      @import '../../styles/global.css';
    </style>
  </head>
  <body class="bg-white dark:bg-novus-gray-900">
    <!-- Navigation -->
    <nav class="fixed top-0 left-0 right-0 z-50 bg-white/80 dark:bg-novus-gray-900/80 backdrop-blur-sm border-b border-novus-gray-200 dark:border-novus-gray-700">
      <div class="max-w-6xl mx-auto px-4 py-4 flex items-center justify-between">
        <a href="/" class="text-2xl font-bold text-novus-gray-900 dark:text-white">
          N<span class="text-novus-red-500">o</span>vus
        </a>
        <div class="flex items-center gap-6">
          <a href="/getting-started/introduction" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Docs</a>
          <a href="/why-novus" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Why Novus?</a>
          <a href="/blog" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">Blog</a>
          <a href="https://github.com/barrylapthorn/novus" class="text-novus-gray-600 dark:text-novus-gray-300 hover:text-novus-red-500 transition-colors">GitHub</a>
        </div>
      </div>
    </nav>

    <main class="pt-24 pb-20 px-4">
      <article class="max-w-3xl mx-auto">
        <header class="mb-8">
          <a href="/blog" class="text-novus-red-500 hover:underline mb-4 inline-block">&larr; Back to Blog</a>
          <h1 class="text-4xl font-bold text-novus-gray-900 dark:text-white mb-4">
            {post.data.title}
          </h1>
          <div class="text-novus-gray-500">
            <time>
              {post.data.date.toLocaleDateString('en-US', {
                year: 'numeric',
                month: 'long',
                day: 'numeric'
              })}
            </time>
            <span class="mx-2">·</span>
            <span>{post.data.author}</span>
          </div>
        </header>

        <div class="prose prose-lg dark:prose-invert max-w-none">
          <Content />
        </div>
      </article>
    </main>

    <Footer />
  </body>
</html>
```

**Step 4: Create first blog post**

Create `website/src/content/blog/2026-01-14-introducing-novus.md`:

```markdown
---
title: "Introducing Novus"
description: "A modern systems programming language for the Amiga 68k family."
date: 2026-01-14
author: "Barry Lapthorn"
tags: ["announcement", "release"]
---

Today I'm excited to publicly introduce **Novus**, a new programming language designed specifically for the Amiga 68k family of computers.

## Why Another Language?

If you're developing for the Amiga in 2026, your options are essentially C or assembly. Both have their place, but both come with significant drawbacks:

**C** gives you portability and abstraction, but also null pointer dereferences, buffer overflows, preprocessor madness, and error handling that's an afterthought. On a system without memory protection, these aren't just bugs—they're system crashes.

**Assembly** gives you total control and optimal code, but at the cost of verbosity, no type safety, and a steep learning curve.

Novus aims to be a third option: a language with modern safety features and clean syntax that still gives you the low-level control you need for systems programming.

## What Makes Novus Different?

### Safety Without Overhead

Novus uses `Result` and `Option` types instead of null pointers and error codes. The compiler ensures you handle errors explicitly:

```novus
let file = open("data.txt")?  // Propagates error if open fails
let data = read_all(file)?
```

No more forgetting to check return values. No more null pointer crashes.

### Clean, Modern Syntax

No preprocessor. No header files. Type inference where it makes sense. Pattern matching for complex logic:

```novus
match command {
    "quit" => break,
    "help" => show_help(),
    _ => print("Unknown command"),
}
```

### Amiga Native

Novus isn't a port of something else. It's designed for Amiga from the ground up:

- Proper AmigaOS ABI compliance
- Direct library and device access
- Hardware register access for demos and games
- Async primitives built on Exec signals

### Readable Output

The compiler generates clean, readable 68k assembly. When you need to debug at the metal, you can actually understand what the compiler produced.

## Current Status

Novus is in active development. The compiler currently supports:

- Core language features (variables, functions, control flow, structs, enums)
- Strong typing with type inference
- Result and Option types
- Pattern matching
- CPU-target aware code generation (68000–68060)
- AmigaOS library integration

It produces working Amiga executables that run on real hardware and emulators.

## What's Next?

The roadmap includes:

- Hardware DSLs for Copper, Blitter, and Paula
- More standard library coverage
- Async/await for cooperative multitasking
- Fat binary support for multi-CPU targets

## Try It Out

Novus is open source and available on [GitHub](https://github.com/barrylapthorn/novus).

The [Getting Started guide](/getting-started/introduction) will help you set up the toolchain and write your first program.

I'd love to hear your feedback. File issues on GitHub or reach out if you have questions.

*New code for classic machines.*
```

**Step 5: Install prose plugin for blog styling**

```bash
cd /Users/barry/RiderProjects/Novus/website
npm install @tailwindcss/typography
```

Update `website/tailwind.config.mjs` to add the typography plugin:

```javascript
import starlightPlugin from '@astrojs/starlight-tailwind';
import typography from '@tailwindcss/typography';

/** @type {import('tailwindcss').Config} */
export default {
  content: ['./src/**/*.{astro,html,js,jsx,md,mdx,svelte,ts,tsx,vue}'],
  theme: {
    extend: {
      colors: {
        'novus-red': {
          50: '#fef2f2',
          100: '#fee2e2',
          200: '#fecaca',
          300: '#fca5a5',
          400: '#f87171',
          500: '#E63030',
          600: '#dc2626',
          700: '#b91c1c',
          800: '#991b1b',
          900: '#7f1d1d',
          950: '#450a0a',
        },
        accent: {
          DEFAULT: 'var(--sl-color-accent)',
          light: 'var(--sl-color-accent-light)',
          dark: 'var(--sl-color-accent-dark)',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'Fira Code', 'monospace'],
      },
    },
  },
  plugins: [starlightPlugin(), typography],
};
```

**Step 6: Verify blog**

```bash
cd /Users/barry/RiderProjects/Novus/website
npm run dev
```

Visit http://localhost:4321/blog - should see blog index with the first post.

**Step 7: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add blog with first post"
```

---

## Phase 5: Final Polish

### Task 11: Add Favicon and Meta Tags

**Files:**
- Create: `website/public/favicon.svg`
- Modify: `website/src/pages/index.astro` (add meta tags)

**Step 1: Create favicon**

Create `website/public/favicon.svg`:

```svg
<svg width="32" height="32" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
  <circle cx="16" cy="16" r="15" fill="#E63030"/>
  <circle cx="16" cy="16" r="15" fill="url(#stripes)" fill-opacity="0.3"/>
  <text x="16" y="22" text-anchor="middle" fill="white" font-family="Inter, Arial, sans-serif" font-weight="bold" font-size="18">N</text>
  <defs>
    <pattern id="stripes" patternUnits="userSpaceOnUse" width="8" height="8" patternTransform="rotate(45)">
      <rect width="4" height="8" fill="white"/>
    </pattern>
  </defs>
</svg>
```

**Step 2: Update pages with complete meta tags**

The homepage and other pages should include:
- Open Graph tags
- Twitter card tags
- Favicon link

Add to the `<head>` of `website/src/pages/index.astro`:

```html
<link rel="icon" type="image/svg+xml" href="/favicon.svg" />
<meta property="og:title" content="Novus - New code for classic machines" />
<meta property="og:description" content="A modern systems programming language for the Amiga 68k" />
<meta property="og:type" content="website" />
<meta property="og:url" content="https://novuslang.com" />
<meta name="twitter:card" content="summary" />
<meta name="twitter:title" content="Novus - New code for classic machines" />
<meta name="twitter:description" content="A modern systems programming language for the Amiga 68k" />
```

**Step 3: Commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): add favicon and meta tags"
```

---

### Task 12: Build and Test

**Files:**
- None (verification only)

**Step 1: Run production build**

```bash
cd /Users/barry/RiderProjects/Novus/website
npm run build
```

Expected: Build completes without errors, output in `dist/` directory.

**Step 2: Preview production build**

```bash
npm run preview
```

Visit http://localhost:4321 and verify:
- Homepage loads with all sections
- Navigation works
- Docs pages load with sidebar
- Blog index and posts work
- Dark mode toggle works (if present)
- All links work

**Step 3: Final commit**

```bash
cd /Users/barry/RiderProjects/Novus
git add website/
git commit -m "feat(website): complete initial website implementation"
```

---

## Summary

This plan creates a complete Novus language website with:

1. **Project setup** — Astro + Starlight + Tailwind
2. **Boing Ball theming** — Red/white color palette with warm neutrals
3. **Homepage** — Hero, value props, code showcase
4. **Why Novus page** — Philosophy and comparison
5. **Getting Started docs** — Introduction, installation, first program
6. **Language Guide** — Variables, functions, control flow, errors, memory
7. **Reference docs** — CLI reference, language reference stub
8. **Blog** — Infrastructure and first post

All content lives in `/website` subdirectory of the Novus repo.
