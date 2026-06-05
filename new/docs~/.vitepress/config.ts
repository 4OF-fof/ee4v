import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: 'ee4v Docs',
  description: 'EditorExtensions for VRChat',
  srcDir: 'src',
  markdown: {
    config(md) {
      const defaultFence = md.renderer.rules.fence

      md.renderer.rules.fence = (tokens, idx, options, env, self) => {
        const token = tokens[idx]
        const language = token.info.trim().split(/\s+/)[0]

        if (language === 'mermaid') {
          return `<ClientOnly><MermaidDiagram code="${encodeURIComponent(token.content)}" /></ClientOnly>`
        }

        return defaultFence
          ? defaultFence(tokens, idx, options, env, self)
          : self.renderToken(tokens, idx, options)
      }
    }
  },
  themeConfig: {
    // https://vitepress.dev/reference/default-theme-config
    nav: [
      { text: 'Overview', link: '/' },
      { text: 'Core', link: '/core/' },
      { text: 'UI', link: '/ui/' },
      { text: 'AssetManager', link: '/asset-manager/' }
    ],

    sidebar: [
      {
        text: 'Core',
        items: [
          { text: 'Overview', link: '/core/' },
          { text: 'Internal', link: '/core/internal' },
          { text: 'Injector', link: '/core/injector' },
          { text: 'Settings', link: '/core/settings' },
          { text: 'Localization', link: '/core/localization' },
          { text: 'Testing', link: '/core/testing' },
          { text: 'Checklist', link: '/core/checklist' }
        ]
      },
      {
        text: 'UI',
        items: [
          { text: 'Overview', link: '/ui/' },
          { text: 'Rules', link: '/ui/rules' },
          { text: 'Catalog', link: '/ui/catalog' },
          { text: 'Checklist', link: '/ui/checklist' }
        ]
      },
      {
        text: 'AssetManager',
        items: [
          { text: 'Overview', link: '/asset-manager/' },
          { text: 'DB Schema', link: '/asset-manager/schema' },
          { text: 'Data Elements', link: '/asset-manager/data-elements' },
          { text: 'Roadmap', link: '/asset-manager/roadmap' },
          { text: 'BLM data.db', link: '/datasource/blm_db_structure' }
        ]
      },
      {
        text: 'Maintenance',
        items: [
          { text: 'Overview', link: '/maintenance/' },
          { text: 'Unity Upgrade', link: '/maintenance/unity-upgrade' }
        ]
      }
    ]
  }
})
