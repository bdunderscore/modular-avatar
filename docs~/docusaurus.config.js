// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const themes = require('prism-react-renderer').themes;
const lightCodeTheme = themes.github;
const darkCodeTheme = themes.dracula;

const defaultLocale = 'en';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Modular Avatar',
  tagline: 'Drag-and-drop avatar assembly',
  url: 'https://modular-avatar.nadena.dev',
  baseUrl: '/',
  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',
  favicon: 'img/favicon.ico.png',

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'bdunderscore', // Usually your GitHub org/user name.
  projectName: 'modular-avatar', // Usually your repo name.
  trailingSlash: false,

  // Even if you don't use internalization, you can use this field to set useful
  // metadata like html lang. For example, if your site is Chinese, you may want
  // to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale,
    locales: ['en','ja'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: require.resolve('./sidebars.js'),
          editUrl: ({locale, docPath}) => {
            if (locale === defaultLocale) {
              return `https://github.com/bdunderscore/modular-avatar/tree/main/docs~/docs/${docPath}`;
            } else {
              return `https://github.com/bdunderscore/modular-avatar/tree/main/docs~/i18n/${locale}/docusaurus-plugin-content-docs/current/${docPath}`;
            }
          },
        },
        blog: false,
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      navbar: {
        title: 'Modular Avatar documentation',
        logo: {
          alt: 'Logo',
          src: 'img/logo/ma_logo.png',
        },
        items: [
          {
            type: 'doc',
            docId: 'intro',
            position: 'left',
            label: 'Docs',
          },
          {
              type: 'localeDropdown',
              position: 'left',
          },
          {
            href: 'https://github.com/bdunderscore/modular-avatar',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            label: 'Documentation',
            to: '/docs/intro',
          },
          {
            label: 'GitHub',
            href: 'https://github.com/bdunderscore/modular-avatar',
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} bd_. Built with Docusaurus.`,
      },
      prism: {
        theme: lightCodeTheme,
        darkTheme: darkCodeTheme,
      },
    }),
};

module.exports = config;
