// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

import tailwindcss from '@tailwindcss/vite';

// https://astro.build/config
export default defineConfig({
  site: 'https://novuslang.com',
  integrations: [
      starlight({
          title: 'Novus',
          tagline: 'New code for classic machines',
          expressiveCode: {
            themes: ['github-dark'],
          },
          logo: {
            light: './src/assets/logo-light.svg',
            dark: './src/assets/logo-dark.svg',
            replacesTitle: false,
          },
          social: [
            { icon: 'github', label: 'GitHub', href: 'https://github.com/barryw/novus' },
          ],
          customCss: [
              './src/styles/global.css',
          ],
          head: [
            {
              tag: 'link',
              attrs: { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
            },
            {
              tag: 'link',
              attrs: { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
            },
            {
              tag: 'link',
              attrs: { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: true },
            },
            {
              tag: 'link',
              attrs: {
                rel: 'stylesheet',
                href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap',
              },
            },
          ],
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
      }),
	],

  vite: {
    plugins: [tailwindcss()],
  },
});